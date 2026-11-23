using ONA.Analysis.Application.Features.AnalysisRuns.Commands;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Application.Services;
using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Application.Tests;

/// <summary>Kafka-triggered analysis (GraphVersionPublished) and the AnalysisCompleted event.</summary>
public sealed class GraphVersionTriggerTests
{
    private readonly InMemoryAnalysisRunRepository _repository = new();
    private readonly FakeGraphSnapshotPort _port = new();
    private readonly FakeEventPublisher _publisher = new();

    private static GraphSnapshot Chain(string boardId, string version = "v1") =>
        new(boardId, version,
            [new SnapshotNode("a", "Ann"), new SnapshotNode("b", "Bob"), new SnapshotNode("c", "Cid")],
            [new SnapshotConnection("a", "b", 1), new SnapshotConnection("b", "c", 1)]);

    private AnalysisRunService Service() => new(_repository, _port, _publisher, TimeProvider.System);

    private StartAnalysisForGraphVersionCommandHandler TriggerHandler() => new(Service());

    [Fact]
    public async Task GraphVersionPublished_RunsAnalysisOnceAndPublishesOneEvent()
    {
        _port.Handler = id => Chain(id);

        var outcome = await TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v1"));

        var run = Assert.Single(_repository.Runs);
        Assert.Equal(GraphVersionAnalysisOutcome.Analyzed, outcome.Status);
        Assert.Equal(run.Id, outcome.AnalysisRunId);
        var published = Assert.Single(_publisher.Published);
        Assert.Equal((run.Id, "board-1", "v1"), (published.AnalysisRunId, published.BoardId, published.GraphVersionId));
        Assert.False(string.IsNullOrWhiteSpace(published.EventId));
    }

    [Fact]
    public async Task SecondDeliveryOfSameGraphVersion_IsNoOp()
    {
        _port.Handler = id => Chain(id);
        await TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v1"));

        var outcome = await TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v1"));

        Assert.Equal(GraphVersionAnalysisOutcome.AlreadyAnalyzed, outcome.Status);
        Assert.Single(_repository.Runs);
        Assert.Single(_publisher.Published);
    }

    [Fact]
    public async Task EventForOlderVersion_WhenCurrentVersionAlreadyAnalyzed_IsNoOp()
    {
        _port.Handler = id => Chain(id, "v2");
        await TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v2"));

        var outcome = await TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v1"));

        Assert.Equal(GraphVersionAnalysisOutcome.AlreadyAnalyzed, outcome.Status);
        Assert.Single(_repository.Runs);
    }

    [Fact]
    public async Task UnpublishedBoard_IsSkippedWithoutThrowing()
    {
        _port.Handler = id => throw new GraphNotPublishedException(id);

        var outcome = await TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v1"));

        Assert.Equal(GraphVersionAnalysisOutcome.Skipped, outcome.Status);
        Assert.Contains("analysis_run.graph_not_published", outcome.Reason);
        Assert.Empty(_repository.Runs);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task GraphIntegrationFailure_Propagates_SoTheConsumerDoesNotCommit()
    {
        _port.Handler = _ => throw new GraphIntegrationException("graph down");

        await Assert.ThrowsAsync<GraphIntegrationException>(
            () => TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v1")));

        Assert.Empty(_repository.Runs);
    }

    [Fact]
    public async Task PublisherFailure_DoesNotFailTheRun()
    {
        _port.Handler = id => Chain(id);
        _publisher.ThrowOnPublish = true;

        var outcome = await TriggerHandler().HandleAsync(new StartAnalysisForGraphVersionCommand("board-1", "v1"));

        Assert.Equal(GraphVersionAnalysisOutcome.Analyzed, outcome.Status);
        Assert.Single(_repository.Runs);
    }

    [Fact]
    public async Task HttpStartCommand_AlsoPublishesAnalysisCompleted()
    {
        _port.Handler = id => Chain(id);

        var dto = await new StartAnalysisRunCommandHandler(Service()).HandleAsync(new StartAnalysisRunCommand("board-1"));

        var published = Assert.Single(_publisher.Published);
        Assert.Equal(dto.Id, published.AnalysisRunId);
    }

    [Fact]
    public async Task HttpStartCommand_PublisherFailure_StillReturnsRun()
    {
        _port.Handler = id => Chain(id);
        _publisher.ThrowOnPublish = true;

        var dto = await new StartAnalysisRunCommandHandler(Service()).HandleAsync(new StartAnalysisRunCommand("board-1"));

        Assert.Equal("Completed", dto.Status);
    }
}
