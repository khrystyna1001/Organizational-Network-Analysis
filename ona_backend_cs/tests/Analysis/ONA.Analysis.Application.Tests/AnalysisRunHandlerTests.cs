using ONA.Analysis.Application.Features.AnalysisRuns.Commands;
using ONA.Analysis.Application.Features.AnalysisRuns.Queries;
using ONA.Analysis.Application.Notifications;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Application.Services;
using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Application.Tests;

public sealed class AnalysisRunHandlerTests
{
    private readonly InMemoryAnalysisRunRepository _repository = new();
    private readonly FakeGraphSnapshotPort _port = new();
    private readonly FakeEventPublisher _publisher = new();
    private readonly RecordingPublisher _notifications = new();

    private static GraphSnapshot Chain(string boardId) =>
        new(boardId, "version-1",
            [new SnapshotNode("a", "Ann"), new SnapshotNode("b", "Bob"), new SnapshotNode("c", "Cid")],
            [new SnapshotConnection("a", "b", 1), new SnapshotConnection("b", "c", 1)]);

    private StartAnalysisRunCommandHandler StartHandler() =>
        new(new AnalysisRunService(_repository, _port, _publisher, _notifications, TimeProvider.System));

    [Fact]
    public async Task Start_StoresCompletedRunBoundToGraphVersion()
    {
        _port.Handler = Chain;

        var dto = await StartHandler().Handle(new StartAnalysisRunCommand("board-1"), default);

        Assert.Equal("Completed", dto.Status);
        Assert.Equal("board-1", dto.BoardId);
        Assert.Equal("version-1", dto.GraphVersionId);
        Assert.Equal(3, dto.Metrics!.NodeCount);
        Assert.Equal(["b"], dto.Bottlenecks);
        Assert.Single(_repository.Runs);
    }

    [Fact]
    public async Task Start_PublishesRunCompletedNotificationForTheUi()
    {
        _port.Handler = Chain;

        var dto = await StartHandler().Handle(new StartAnalysisRunCommand("board-1"), default);

        var notification = Assert.IsType<AnalysisRunCompletedNotification>(Assert.Single(_notifications.Published));
        Assert.Equal((dto.Id, "board-1", "version-1", 1),
            (notification.AnalysisRunId, notification.BoardId, notification.GraphVersionId, notification.BottleneckCount));
    }

    [Fact]
    public async Task Start_WhenNotificationHandlerFails_StillReturnsTheSavedRun()
    {
        _port.Handler = Chain;
        _notifications.ThrowOnPublish = true;

        var dto = await StartHandler().Handle(new StartAnalysisRunCommand("board-1"), default);

        Assert.Equal("Completed", dto.Status);
        Assert.Single(_repository.Runs);
    }

    [Fact]
    public async Task Start_WhenGraphNotPublished_ThrowsAndStoresNothing()
    {
        _port.Handler = id => throw new GraphNotPublishedException(id);

        var exception = await Assert.ThrowsAsync<GraphNotPublishedException>(
            () => StartHandler().Handle(new StartAnalysisRunCommand("board-1"), default));

        Assert.Equal("analysis_run.graph_not_published", exception.Code);
        Assert.Empty(_repository.Runs);
    }

    [Fact]
    public async Task Start_WhenBoardMissing_ThrowsBoardNotFound()
    {
        _port.Handler = id => throw new BoardNotFoundException(id);

        var exception = await Assert.ThrowsAsync<BoardNotFoundException>(
            () => StartHandler().Handle(new StartAnalysisRunCommand("nope"), default));

        Assert.Equal("analysis_run.board_not_found", exception.Code);
    }

    [Fact]
    public async Task Start_WhenSnapshotIsInvalid_ThrowsInvalidGraph()
    {
        _port.Handler = id => new GraphSnapshot(id, "v", [new SnapshotNode("a", "A")], [new SnapshotConnection("a", "zzz", 1)]);

        await Assert.ThrowsAsync<InvalidNetworkGraphException>(
            () => StartHandler().Handle(new StartAnalysisRunCommand("board-1"), default));

        Assert.Empty(_repository.Runs);
    }

    [Fact]
    public async Task Start_WithEmptyBoardId_ThrowsInvalidIdentifier()
    {
        await Assert.ThrowsAsync<InvalidIdentifierException>(
            () => StartHandler().Handle(new StartAnalysisRunCommand(" "), default));
    }

    [Fact]
    public async Task GetById_ReturnsRunOrNull()
    {
        _port.Handler = Chain;
        var created = await StartHandler().Handle(new StartAnalysisRunCommand("board-1"), default);
        var handler = new GetAnalysisRunByIdQueryHandler(_repository);

        var found = await handler.Handle(new GetAnalysisRunByIdQuery(created.Id), default);
        var missing = await handler.Handle(new GetAnalysisRunByIdQuery("unknown"), default);

        Assert.Equal(created.Id, found!.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetAll_FiltersByBoard()
    {
        _port.Handler = Chain;
        await StartHandler().Handle(new StartAnalysisRunCommand("board-1"), default);
        await StartHandler().Handle(new StartAnalysisRunCommand("board-2"), default);
        var handler = new GetAnalysisRunsQueryHandler(_repository);

        var all = await handler.Handle(new GetAnalysisRunsQuery(null), default);
        var one = await handler.Handle(new GetAnalysisRunsQuery("board-2"), default);

        Assert.Equal(2, all.Count);
        Assert.Equal("board-2", Assert.Single(one).BoardId);
    }
}
