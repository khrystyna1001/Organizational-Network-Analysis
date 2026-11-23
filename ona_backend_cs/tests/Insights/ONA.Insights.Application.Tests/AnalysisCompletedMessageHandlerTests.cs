using ONA.Insights.Application.Events;
using ONA.Insights.Application.Features.Recommendations.Commands;
using ONA.Insights.Application.Features.Recommendations.Events;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Tests;

public sealed class AnalysisCompletedMessageHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);

    private sealed class UnreachableAnalysisPort : IAnalysisResultsPort
    {
        public Task<AnalysisRunResult?> GetRunAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken = default) =>
            throw new AnalysisIntegrationException("Analysis service is unreachable.");
    }

    private readonly InMemoryRecommendationRepository _repo = new();

    private AnalysisCompletedMessageHandler HandlerFor(IAnalysisResultsPort port) =>
        new(new GenerateRecommendationsCommandHandler(_repo, port, new FakeInsightGenerator(), new FixedClock(Now)));

    private static string Json(string runId) =>
        $$"""{"event_id":"e1","occurred_at":"2026-11-23T12:00:00Z","analysis_run_id":"{{runId}}","board_id":"b1","graph_version_id":"v1"}""";

    [Fact]
    public async Task CompletedRunWithBottlenecks_IsHandledAndCreatesRecommendations()
    {
        var handler = HandlerFor(new FakeAnalysisResultsPort(Runs.Completed("run1", "n2", "n3")));

        var result = await handler.HandleAsync(Json("run1"));

        Assert.Equal(MessageOutcome.Handled, result.Outcome);
        Assert.Equal(2, _repo.Items.Count);
    }

    [Fact]
    public async Task RedeliveredEvent_DoesNotCreateDuplicates()
    {
        var handler = HandlerFor(new FakeAnalysisResultsPort(Runs.Completed("run1", "n2", "n3")));

        await handler.HandleAsync(Json("run1"));
        var second = await handler.HandleAsync(Json("run1"));

        Assert.Equal(MessageOutcome.Handled, second.Outcome);
        Assert.Equal(2, _repo.Items.Count);
    }

    [Fact]
    public async Task RunWithoutBottlenecks_IsSkipped()
    {
        var handler = HandlerFor(new FakeAnalysisResultsPort(Runs.Completed("run1")));

        var result = await handler.HandleAsync(Json("run1"));

        Assert.Equal(MessageOutcome.Skipped, result.Outcome);
        Assert.Contains("recommendation.no_bottlenecks", result.Reason);
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task RunNotCompleted_IsSkipped()
    {
        var running = Runs.Completed("run1", "n1") with { Status = AnalysisRunStatus.Running };
        var handler = HandlerFor(new FakeAnalysisResultsPort(running));

        var result = await handler.HandleAsync(Json("run1"));

        Assert.Equal(MessageOutcome.Skipped, result.Outcome);
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task UnknownRun_IsSkipped()
    {
        var handler = HandlerFor(new FakeAnalysisResultsPort(null));

        var result = await handler.HandleAsync(Json("missing"));

        Assert.Equal(MessageOutcome.Skipped, result.Outcome);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData(null)]
    public async Task InvalidPayload_IsSkipped(string? payload)
    {
        var handler = HandlerFor(new FakeAnalysisResultsPort(Runs.Completed("run1", "n1")));

        var result = await handler.HandleAsync(payload);

        Assert.Equal(MessageOutcome.Skipped, result.Outcome);
        Assert.Empty(_repo.Items);
    }

    [Fact]
    public async Task AnalysisUnreachable_AsksForRetry()
    {
        var handler = HandlerFor(new UnreachableAnalysisPort());

        var result = await handler.HandleAsync(Json("run1"));

        Assert.Equal(MessageOutcome.Retry, result.Outcome);
        Assert.Empty(_repo.Items);
    }
}

public sealed class AnalysisCompletedMessageParsingTests
{
    [Fact]
    public void TryParse_ValidSnakeCaseJson_ReturnsMessage()
    {
        var message = AnalysisCompletedMessage.TryParse(
            """{"event_id":"e1","occurred_at":"2026-11-23T12:00:00Z","analysis_run_id":"run1","board_id":"b1","graph_version_id":"v1"}""");

        Assert.NotNull(message);
        Assert.Equal("run1", message.AnalysisRunId);
        Assert.Equal("b1", message.BoardId);
        Assert.Equal("v1", message.GraphVersionId);
        Assert.Equal("e1", message.EventId);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("""{"analysis_run_id":"  "}""")]
    [InlineData("null")]
    public void TryParse_InvalidJson_ReturnsNull(string json) =>
        Assert.Null(AnalysisCompletedMessage.TryParse(json));
}
