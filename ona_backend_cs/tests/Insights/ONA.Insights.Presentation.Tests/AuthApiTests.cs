using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Presentation.Tests;

/// <summary>HTTP-level auth tests: real pipeline, fake repository and ports (no MySQL, no Kafka).</summary>
public sealed class AuthApiTests : IClassFixture<AuthApiTests.Factory>
{
    private const string Key = "ona-dev-signing-key-change-me-0123456789abcdef";

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:AutoCreate"] = "false",
                ["Kafka:BootstrapServers"] = ""
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRecommendationRepository>();
                services.AddSingleton<IRecommendationRepository, InMemoryRepository>();
                services.RemoveAll<IAnalysisResultsPort>();
                services.AddSingleton<IAnalysisResultsPort, FakeAnalysisPort>();
            });
        }
    }

    private sealed class InMemoryRepository : IRecommendationRepository
    {
        private readonly List<Recommendation> _items = [];

        public Task<Recommendation?> GetByIdAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_items.FirstOrDefault(r => r.Id.Equals(id)));

        public Task<Recommendation?> GetByIdReadOnlyAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
            GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<Recommendation>> ListAsync(
            AnalysisRunId? analysisRunId, RecommendationStatus? status, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Recommendation>>(_items
                .Where(r => analysisRunId is null || r.AnalysisRunId.Equals(analysisRunId))
                .Where(r => status is null || r.Status == status).ToList());

        public Task AddRangeAsync(IReadOnlyCollection<Recommendation> recommendations, CancellationToken cancellationToken = default)
        {
            _items.AddRange(recommendations);
            return Task.CompletedTask;
        }

        public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAnalysisPort : IAnalysisResultsPort
    {
        public Task<AnalysisRunResult?> GetRunAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken = default)
        {
            var nodes = new List<AnalysisNodeResult>
            {
                new(new TargetNodeId("n1"), "Alice", 1, 3, 0.4, true),
                new(new TargetNodeId("n2"), "Bob", 1, 0, 0.0, false)
            };
            return Task.FromResult<AnalysisRunResult?>(new AnalysisRunResult(
                analysisRunId, new BoardId("b1"), new GraphVersionId("v1"), AnalysisRunStatus.Completed, nodes));
        }
    }

    private readonly Factory _factory;

    public AuthApiTests(Factory factory) => _factory = factory;

    private static string Token(string role, string sub = "user-42") =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ona-identity",
            Audience = "ona",
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = new Dictionary<string, object> { ["sub"] = sub, ["name"] = "Tester", ["role"] = role },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256)
        });

    private HttpClient Client(string? role = null, string sub = "user-42")
    {
        var client = _factory.CreateClient();
        if (role is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role, sub));
        }

        return client;
    }

    private static async Task<string> GenerateAsync(HttpClient client, string runId)
    {
        var response = await client.PostAsJsonAsync("/api/v1/recommendations", new { analysis_run_id = runId });
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<JsonElement>();
        return items[0].GetProperty("id").GetString()!;
    }

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Anonymous_IsUnauthorized()
    {
        var response = await Client().GetAsync("/api/v1/recommendations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_StaysAnonymous()
    {
        var response = await Client().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsUnauthorized()
    {
        var client = _factory.CreateClient();
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ona-identity",
            Audience = "ona",
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = new Dictionary<string, object> { ["sub"] = "x", ["role"] = "Admin" },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-signing-key-0000")),
                SecurityAlgorithms.HmacSha256)
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await client.GetAsync("/api/v1/recommendations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ServiceToken_CanReadAndGenerate()
    {
        var client = Client("Service", "insights-service");

        var id = await GenerateAsync(client, "run-svc");
        var response = await client.GetAsync($"/api/v1/recommendations/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ServiceToken_CannotRespond()
    {
        var service = Client("Service", "insights-service");
        var id = await GenerateAsync(service, "run-forbidden");

        var response = await service.PostAsync($"/api/v1/recommendations/{id}/respond", Json(new { response = "approve" }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UserToken_Responds_AndSubClaimIsRecorded()
    {
        var user = Client("User", "user-42");
        var id = await GenerateAsync(user, "run-user");

        // user_id in the body must be ignored in favour of the token's sub claim
        var response = await user.PostAsync(
            $"/api/v1/recommendations/{id}/respond",
            Json(new { response = "approve", comment = "ok", user_id = "someone-else" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", body.GetProperty("status").GetString());
        Assert.Equal("user-42", body.GetProperty("responded_by_user_id").GetString());
    }

    [Fact]
    public async Task AdminToken_CanRespond()
    {
        var admin = Client("Admin", "admin-1");
        var id = await GenerateAsync(admin, "run-admin");

        var response = await admin.PostAsync($"/api/v1/recommendations/{id}/respond", Json(new { response = "reject" }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
