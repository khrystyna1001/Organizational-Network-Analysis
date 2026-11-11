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
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Graph"] = fixture.ConnectionString
                    }));
            });
        using var client = factory.CreateClient();
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
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Graph"] = fixture.ConnectionString
                    }));
            });
        using var client = factory.CreateClient();
        using var boardsBeforeResponse = await client.GetAsync("/api/v1/boards");
        using var boardsBeforeBody = JsonDocument.Parse(
            await boardsBeforeResponse.Content.ReadAsStringAsync());
        var boardCountBefore = boardsBeforeBody.RootElement.GetArrayLength();
        using var form = new MultipartFormDataContent();
        const string csv = """
            EmpID,FirstName,LastName,Title,DepartmentType,Supervisor
            100,Alex,One,Director,Management,missing
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
}
