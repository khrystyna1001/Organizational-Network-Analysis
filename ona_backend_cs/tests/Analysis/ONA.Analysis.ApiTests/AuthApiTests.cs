using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ONA.Analysis.Application.Abstractions;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Domain.Aggregates;

namespace ONA.Analysis.ApiTests;

/// <summary>HTTP pipeline with fake repository + fake Graph port (no MySQL, no Kafka).</summary>
public sealed class AuthApiTests : IClassFixture<AuthApiTests.Factory>
{
    private const string Key = "ona-api-test-signing-key-0123456789abcdef-012345";

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Key,
                ["Jwt:Issuer"] = "ona-identity",
                ["Jwt:Audience"] = "ona",
                ["Database:InitializeOnStartup"] = "false",
                ["ConnectionStrings:Analysis"] = "Server=unused;Database=unused",
                ["Kafka:BootstrapServers"] = ""
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAnalysisRunRepository>();
                services.AddSingleton<IAnalysisRunRepository, FakeRepository>();
                services.RemoveAll<IGraphSnapshotPort>();
                services.AddSingleton<IGraphSnapshotPort, FakeGraph>();
            });
        }
    }

    private sealed class FakeRepository : IAnalysisRunRepository
    {
        private readonly List<AnalysisRun> _runs = [];

        public Task AddAsync(AnalysisRun run, CancellationToken ct = default) { _runs.Add(run); return Task.CompletedTask; }
        public Task<AnalysisRun?> FindCompletedByGraphVersionAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_runs.FirstOrDefault(r => r.GraphVersionId == id));
        public Task<AnalysisRun?> GetByIdReadOnlyAsync(string id, CancellationToken ct = default) =>
            Task.FromResult(_runs.FirstOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<AnalysisRun>> GetAllReadOnlyAsync(string? boardId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AnalysisRun>>(_runs.Where(r => boardId is null || r.BoardId == boardId).ToList());
    }

    private sealed class FakeGraph : IGraphSnapshotPort
    {
        public Task<GraphSnapshot> GetSnapshotAsync(string boardId, CancellationToken ct = default) =>
            Task.FromResult(new GraphSnapshot(boardId, "v1",
                [new SnapshotNode("a", "Ann"), new SnapshotNode("b", "Bob"), new SnapshotNode("c", "Cid")],
                [new SnapshotConnection("a", "b", 1), new SnapshotConnection("b", "c", 1)]));
    }

    private readonly Factory _factory;

    public AuthApiTests(Factory factory) => _factory = factory;

    private static string Token(string role = "User", string key = Key, TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ona-identity",
            Audience = "ona",
            NotBefore = now.AddMinutes(-1),
            Expires = now.Add(lifetime ?? TimeSpan.FromMinutes(5)),
            Claims = new Dictionary<string, object> { ["sub"] = "u1", ["role"] = role },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        });
    }

    private HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    [Fact]
    public async Task Health_IsAnonymous() =>
        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/")).StatusCode);

    [Fact]
    public async Task Get_WithoutToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/v1/analysis-runs")).StatusCode);

    [Fact]
    public async Task Post_WithoutToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client().PostAsJsonAsync("/api/v1/analysis-runs", new { board_id = "b1" })).StatusCode);

    [Fact]
    public async Task Get_WithTokenSignedByOtherKey_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client(Token(key: "another-signing-key-0123456789abcdef-0123456789")).GetAsync("/api/v1/analysis-runs")).StatusCode);

    [Fact]
    public async Task Get_WithExpiredToken_Returns401() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client(Token(lifetime: TimeSpan.FromMinutes(-30))).GetAsync("/api/v1/analysis-runs")).StatusCode);

    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    [InlineData("Service")]
    public async Task ValidToken_AnyRole_CanStartAndReadRun(string role)
    {
        var client = Client(Token(role));

        var created = await client.PostAsJsonAsync("/api/v1/analysis-runs", new { board_id = "board-" + role });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var run = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var id = run.GetProperty("id").GetString();

        var read = await client.GetAsync($"/api/v1/analysis-runs/{id}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }
}
