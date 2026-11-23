using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ONA.Analysis.Application.Abstractions.Messaging;
using ONA.Analysis.Application.Features.AnalysisRuns.Commands;
using ONA.Analysis.Infrastructure.Acl;
using ONA.Analysis.Infrastructure.Acl.Events;
using ONA.Analysis.Infrastructure.Messaging;

namespace ONA.Analysis.Infrastructure.Tests;

public sealed class AuthAndMessagingTests
{
    private const string Key = "ona-test-signing-key-0123456789abcdef-0123456789";

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Jwt:Key"] = Key, ["Jwt:Issuer"] = "ona-identity", ["Jwt:Audience"] = "ona"
    }).Build();

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<CapturingHandler> SendThroughAsync(HttpContext? context)
    {
        var terminal = new CapturingHandler();
        var handler = new BearerForwardingHandler(
            new HttpContextAccessor { HttpContext = context },
            new ServiceTokenFactory(Config(), TimeProvider.System)) { InnerHandler = terminal };
        await new HttpClient(handler).GetAsync("http://graph/x");
        return terminal;
    }

    [Fact]
    public async Task Handler_ForwardsIncomingBearerToken()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer user-token-123";

        var terminal = await SendThroughAsync(context);

        Assert.Equal("Bearer", terminal.Last!.Headers.Authorization!.Scheme);
        Assert.Equal("user-token-123", terminal.Last.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Handler_WithoutHttpContext_MintsValidServiceToken()
    {
        var terminal = await SendThroughAsync(null);

        var token = terminal.Last!.Headers.Authorization!.Parameter!;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "ona-identity",
            ValidAudience = "ona",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
            RoleClaimType = "role"
        });
        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("Service", result.ClaimsIdentity.FindFirst("role")!.Value);
        Assert.Equal("analysis-service", result.ClaimsIdentity.FindFirst("sub")!.Value);
    }

    [Fact]
    public void ServiceToken_IsShortLived()
    {
        var token = new JsonWebToken(new ServiceTokenFactory(Config(), TimeProvider.System).Create());

        Assert.InRange((token.ValidTo - token.ValidFrom).TotalMinutes, 4, 6);
    }

    [Fact]
    public void Translator_ParsesValidEvent()
    {
        var ok = GraphEventTranslator.TryParseGraphVersionPublished(
            """{"event_id":"e1","occurred_at":"2026-01-01T00:00:00Z","board_id":"b1","graph_version_id":"v1"}""",
            out var message);

        Assert.True(ok);
        Assert.Equal(("e1", "b1", "v1"), (message.EventId, message.BoardId, message.GraphVersionId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"board_id":"b1"}""")]
    [InlineData("""{"board_id":"","graph_version_id":"v1"}""")]
    public void Translator_RejectsInvalidEvent(string? json) =>
        Assert.False(GraphEventTranslator.TryParseGraphVersionPublished(json, out _));

    private sealed class FakeTriggerHandler(Func<StartAnalysisForGraphVersionCommand, GraphVersionAnalysisOutcome> respond)
        : ICommandHandler<StartAnalysisForGraphVersionCommand, GraphVersionAnalysisOutcome>
    {
        public List<StartAnalysisForGraphVersionCommand> Calls { get; } = [];

        public Task<GraphVersionAnalysisOutcome> HandleAsync(StartAnalysisForGraphVersionCommand command, CancellationToken ct = default)
        {
            Calls.Add(command);
            return Task.FromResult(respond(command));
        }
    }

    private static (GraphVersionPublishedProcessor Processor, FakeTriggerHandler Handler) CreateProcessor(
        Func<StartAnalysisForGraphVersionCommand, GraphVersionAnalysisOutcome> respond)
    {
        var handler = new FakeTriggerHandler(respond);
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<StartAnalysisForGraphVersionCommand, GraphVersionAnalysisOutcome>>(handler);
        var provider = services.BuildServiceProvider();
        return (new GraphVersionPublishedProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<GraphVersionPublishedProcessor>.Instance), handler);
    }

    private const string ValidEvent = """{"event_id":"e","board_id":"b1","graph_version_id":"v1"}""";

    [Fact]
    public async Task Processor_ValidMessage_CallsHandlerAndCommits()
    {
        var (processor, handler) = CreateProcessor(_ => new GraphVersionAnalysisOutcome("Analyzed", "r1", null));

        var commit = await processor.ProcessAsync(ValidEvent, CancellationToken.None);

        Assert.True(commit);
        Assert.Equal(("b1", "v1"), (handler.Calls.Single().BoardId, handler.Calls.Single().GraphVersionId));
    }

    [Fact]
    public async Task Processor_PoisonMessage_IsSkippedAndCommitted()
    {
        var (processor, handler) = CreateProcessor(_ => throw new InvalidOperationException("should not be called"));

        var commit = await processor.ProcessAsync("{broken", CancellationToken.None);

        Assert.True(commit);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Processor_SkippedOutcome_IsCommitted()
    {
        var (processor, _) = CreateProcessor(_ => new GraphVersionAnalysisOutcome("Skipped", null, "analysis_run.graph_not_published: x"));

        Assert.True(await processor.ProcessAsync(ValidEvent, CancellationToken.None));
    }

    [Fact]
    public async Task Processor_TechnicalFailure_IsNotCommitted()
    {
        var (processor, _) = CreateProcessor(_ => throw new HttpRequestException("graph down"));

        Assert.False(await processor.ProcessAsync(ValidEvent, CancellationToken.None));
    }
}
