using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ONA.Graph.Infrastructure.IntegrationTests;

[Collection(MySqlCollection.Name)]
public sealed class AuthApiTests(MySqlDatabaseFixture fixture)
{
    [Fact]
    public async Task AnonymousRequestToBoardsReturns401()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpointStaysAnonymous()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserTokenCannotDeleteBoardButAdminCan()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var user = factory.AuthorizedClient("User");
        using var admin = factory.AuthorizedClient("Admin");

        using var created = await user.PostAsJsonAsync("/api/v1/boards", new { name = "To delete" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = body.RootElement.GetProperty("id").GetString();

        using var forbidden = await user.DeleteAsync("/api/v1/boards/" + id);
        using var allowed = await admin.DeleteAsync("/api/v1/boards/" + id);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKeyReturns401()
    {
        await fixture.ResetDatabaseAsync();
        using var factory = TestAuth.CreateFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestAuth.Token() + "x");

        using var response = await client.GetAsync("/api/v1/boards");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
