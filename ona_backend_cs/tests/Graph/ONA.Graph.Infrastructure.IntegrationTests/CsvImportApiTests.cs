using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ONA.Graph.Infrastructure.IntegrationTests;

[Collection(MySqlCollection.Name)]
public sealed class CsvImportApiTests(MySqlDatabaseFixture fixture)
{
    [Fact]
    public async Task UploadingEmployeeCsvCreatesBoardNodesAndSupervisorConnections()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.AuthorizedClient();
        using var form = new MultipartFormDataContent();
        var csv = """
            EmpID,FirstName,LastName,Title,DepartmentType,Supervisor
            100,Alex,One,Director,Management,
            101,Sam,Smith,Manager,Engineering,100
            102,Taylor,Jones,Engineer,Engineering,101
            """;
        form.Add(new StringContent(csv), "file", "engineering.csv");
        form.Add(new StringContent("Engineering"), "board_name");

        using var response = await client.PostAsync("/api/v1/data", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        var boardId = root.GetProperty("board_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(boardId));
        Assert.Equal(3, root.GetProperty("total_rows").GetInt32());
        Assert.Equal(3, root.GetProperty("nodes_created").GetInt32());
        Assert.Equal(2, root.GetProperty("connections_created").GetInt32());
        Assert.Equal(3, root.GetProperty("data").GetArrayLength());

        using var nodesResponse = await client.GetAsync("/api/v1/nodes?board_id=" + boardId);
        using var nodesBody = JsonDocument.Parse(await nodesResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, nodesResponse.StatusCode);
        Assert.Equal(3, nodesBody.RootElement.GetArrayLength());
        Assert.Contains(
            nodesBody.RootElement.EnumerateArray(),
            node => node.GetProperty("name").GetString() == "Alex One");

        using var connectionsResponse = await client.GetAsync(
            "/api/v1/connections?board_id=" + boardId);
        using var connectionsBody = JsonDocument.Parse(
            await connectionsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, connectionsResponse.StatusCode);
        Assert.Equal(2, connectionsBody.RootElement.GetArrayLength());
        var employeeNodeIds = nodesBody.RootElement.EnumerateArray()
            .ToDictionary(
                node => node.GetProperty("name").GetString()!,
                node => node.GetProperty("id").GetString()!);
        var alexToSam = connectionsBody.RootElement.EnumerateArray().Single(connection =>
            connection.GetProperty("target_node_id").GetString() == employeeNodeIds["Sam Smith"]);
        Assert.Equal(
            employeeNodeIds["Alex One"],
            alexToSam.GetProperty("source_node_id").GetString());
    }

    [Fact]
    public async Task InvalidSupervisorReferenceRejectsUploadWithoutCreatingBoard()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.AuthorizedClient();
        using var boardsBeforeResponse = await client.GetAsync("/api/v1/boards");
        using var boardsBeforeBody = JsonDocument.Parse(
            await boardsBeforeResponse.Content.ReadAsStringAsync());
        var boardCountBefore = boardsBeforeBody.RootElement.GetArrayLength();
        using var form = new MultipartFormDataContent();
        const string csv = """
            EmpID,FirstName,LastName,Title,DepartmentType,Supervisor
            100,Alex,One,Director,Management,100
            """;
        form.Add(new StringContent(csv), "file", "engineering.csv");
        form.Add(new StringContent("Rejected Import Test"), "board_name");

        using var response = await client.PostAsync("/api/v1/data", form);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "board_import.invalid",
            body.RootElement.GetProperty("code").GetString());
        using var boardsResponse = await client.GetAsync("/api/v1/boards");
        using var boardsBody = JsonDocument.Parse(
            await boardsResponse.Content.ReadAsStringAsync());
        Assert.Equal(boardCountBefore, boardsBody.RootElement.GetArrayLength());
        Assert.DoesNotContain(
            boardsBody.RootElement.EnumerateArray(),
            board => board.GetProperty("name").GetString() == "Rejected Import Test");
    }

    private const string PeopleCsv = "Person,Manager,Role\nAlex One,,CEO\nSam Smith,Alex One,Developer\n";

    [Fact]
    public async Task UploadingAnyCsvWithAColumnMappingCreatesTheBoard()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.AuthorizedClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(PeopleCsv), "file", "people.csv");
        form.Add(new StringContent("Any CSV"), "board_name");
        form.Add(new StringContent("""{"full_name":"Person","title":"Role","supervisor":"Manager","department":""}"""), "mapping");

        using var response = await client.PostAsync("/api/v1/data", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("nodes_created").GetInt32());
        Assert.Equal(1, body.RootElement.GetProperty("connections_created").GetInt32());
        var boardId = body.RootElement.GetProperty("board_id").GetString();
        using var nodes = JsonDocument.Parse(await client.GetStringAsync("/api/v1/nodes?board_id=" + boardId));
        Assert.Contains(nodes.RootElement.EnumerateArray(), n => n.GetProperty("title").GetString() == "Developer");
        Assert.All(nodes.RootElement.EnumerateArray(), n => Assert.Equal("General", n.GetProperty("department").GetString()));
    }

    [Fact]
    public async Task PreviewReturnsColumnsSampleRowsAndASuggestedMappingWithoutSavingAnything()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.AuthorizedClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("Employee ID,Full Name,Job Title,Reports To\n1,Alex One,CEO,\n2,Sam Smith,Dev,Alex One\n"), "file", "people.csv");

        using var response = await client.PostAsync("/api/v1/data/preview", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(4, root.GetProperty("columns").GetArrayLength());
        Assert.Equal(2, root.GetProperty("row_count").GetInt32());
        Assert.Equal(2, root.GetProperty("sample_rows").GetArrayLength());
        var suggested = root.GetProperty("suggested_mapping");
        Assert.Equal("Employee ID", suggested.GetProperty("employee_id").GetString());
        Assert.Equal("Full Name", suggested.GetProperty("full_name").GetString());
        Assert.Equal("Reports To", suggested.GetProperty("supervisor").GetString());
        using var boards = JsonDocument.Parse(await client.GetStringAsync("/api/v1/boards"));
        Assert.DoesNotContain(boards.RootElement.EnumerateArray(), b => b.GetProperty("name").GetString() == "people");
    }

    [Fact]
    public async Task MappingWithoutAnyNameColumnIsRejectedWithItsOwnCode()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.AuthorizedClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(PeopleCsv), "file", "people.csv");
        form.Add(new StringContent("""{"supervisor":"Manager"}"""), "mapping");

        using var response = await client.PostAsync("/api/v1/data", form);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("board_import.invalid_mapping", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task MappingThatIsNotJsonIsABadRequest()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.AuthorizedClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(PeopleCsv), "file", "people.csv");
        form.Add(new StringContent("not json"), "mapping");

        using var response = await client.PostAsync("/api/v1/data", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PreviewWithoutAFileIsABadRequestAndAnonymousIsUnauthorized()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var authorized = factory.AuthorizedClient();
        using var anonymous = factory.CreateClient();

        using var noFile = await authorized.PostAsync("/api/v1/data/preview", new MultipartFormDataContent());
        using var denied = await anonymous.PostAsync("/api/v1/data/preview", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.BadRequest, noFile.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }
}
