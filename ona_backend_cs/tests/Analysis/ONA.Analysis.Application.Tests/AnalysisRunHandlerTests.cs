using ONA.Analysis.Application.Features.AnalysisRuns.Commands;
using ONA.Analysis.Application.Features.AnalysisRuns.Queries;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Application.Services;
using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Application.Tests;

public sealed class AnalysisRunHandlerTests
{
    private readonly InMemoryAnalysisRunRepository _repository = new();
    private readonly FakeGraphSnapshotPort _port = new();
    private readonly FakeEventPublisher _publisher = new();

    private static GraphSnapshot Chain(string boardId) =>
        new(boardId, "version-1",
            [new SnapshotNode("a", "Ann"), new SnapshotNode("b", "Bob"), new SnapshotNode("c", "Cid")],
            [new SnapshotConnection("a", "b", 1), new SnapshotConnection("b", "c", 1)]);

    private StartAnalysisRunCommandHandler StartHandler() =>
        new(new AnalysisRunService(_repository, _port, _publisher, TimeProvider.System));

    [Fact]
    public async Task Start_StoresCompletedRunBoundToGraphVersion()
    {
        _port.Handler = Chain;

        var dto = await StartHandler().HandleAsync(new StartAnalysisRunCommand("board-1"));

        Assert.Equal("Completed", dto.Status);
        Assert.Equal("board-1", dto.BoardId);
        Assert.Equal("version-1", dto.GraphVersionId);
        Assert.Equal(3, dto.Metrics!.NodeCount);
        Assert.Equal(["b"], dto.Bottlenecks);
        Assert.Single(_repository.Runs);
    }

    [Fact]
    public async Task Start_WhenGraphNotPublished_ThrowsAndStoresNothing()
    {
        _port.Handler = id => throw new GraphNotPublishedException(id);

        var exception = await Assert.ThrowsAsync<GraphNotPublishedException>(
            () => StartHandler().HandleAsync(new StartAnalysisRunCommand("board-1")));

        Assert.Equal("analysis_run.graph_not_published", exception.Code);
        Assert.Empty(_repository.Runs);
    }

    [Fact]
    public async Task Start_WhenBoardMissing_ThrowsBoardNotFound()
    {
        _port.Handler = id => throw new BoardNotFoundException(id);

        var exception = await Assert.ThrowsAsync<BoardNotFoundException>(
            () => StartHandler().HandleAsync(new StartAnalysisRunCommand("nope")));

        Assert.Equal("analysis_run.board_not_found", exception.Code);
    }

    [Fact]
    public async Task Start_WhenSnapshotIsInvalid_ThrowsInvalidGraph()
    {
        _port.Handler = id => new GraphSnapshot(id, "v", [new SnapshotNode("a", "A")], [new SnapshotConnection("a", "zzz", 1)]);

        await Assert.ThrowsAsync<InvalidNetworkGraphException>(
            () => StartHandler().HandleAsync(new StartAnalysisRunCommand("board-1")));

        Assert.Empty(_repository.Runs);
    }

    [Fact]
    public async Task Start_WithEmptyBoardId_ThrowsInvalidIdentifier()
    {
        await Assert.ThrowsAsync<InvalidIdentifierException>(
            () => StartHandler().HandleAsync(new StartAnalysisRunCommand(" ")));
    }

    [Fact]
    public async Task GetById_ReturnsRunOrNull()
    {
        _port.Handler = Chain;
        var created = await StartHandler().HandleAsync(new StartAnalysisRunCommand("board-1"));
        var handler = new GetAnalysisRunByIdQueryHandler(_repository);

        var found = await handler.HandleAsync(new GetAnalysisRunByIdQuery(created.Id));
        var missing = await handler.HandleAsync(new GetAnalysisRunByIdQuery("unknown"));

        Assert.Equal(created.Id, found!.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetAll_FiltersByBoard()
    {
        _port.Handler = Chain;
        await StartHandler().HandleAsync(new StartAnalysisRunCommand("board-1"));
        await StartHandler().HandleAsync(new StartAnalysisRunCommand("board-2"));
        var handler = new GetAnalysisRunsQueryHandler(_repository);

        var all = await handler.HandleAsync(new GetAnalysisRunsQuery(null));
        var one = await handler.HandleAsync(new GetAnalysisRunsQuery("board-2"));

        Assert.Equal(2, all.Count);
        Assert.Equal("board-2", Assert.Single(one).BoardId);
    }
}
