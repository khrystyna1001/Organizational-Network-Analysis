using ONA.Graph.Application.Features.Boards.Queries;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Tests.Features;

public sealed class GraphSnapshotQueryTests
{
    private readonly InMemoryBoardRepository _repository = new();

    [Fact]
    public async Task Snapshot_OfPublishedBoard_ReturnsVersionNodesAndConnections()
    {
        var board = _repository.Seed(new Board("b1", "Engineering", null));
        var boss = board.AddNode("Alex", "Director", "Engineering");
        var dev = board.AddNode("Sam", "Developer", "Engineering");
        board.AddConnection(boss.Id, dev.Id, 2.0);
        board.BuildNetworkGraph();
        var versionId = board.PublishGraphVersion();

        var result = await new GetGraphSnapshotQueryHandler(_repository)
            .Handle(new GetGraphSnapshotQuery("b1"), default);

        Assert.Equal(versionId, result.GraphVersionId);
        Assert.Equal(2, result.Nodes.Count);
        var connection = Assert.Single(result.Connections);
        Assert.Equal(boss.Id, connection.SourceNodeId);
        Assert.Equal(2.0, connection.Weight);
    }

    [Fact]
    public async Task Snapshot_OfDraftBoard_ThrowsNotPublished()
    {
        _repository.Seed(new Board("b1", "Engineering", null)).AddNode("Alex", "Director", "Engineering");

        var exception = await Assert.ThrowsAsync<BoardNotPublishedException>(
            () => new GetGraphSnapshotQueryHandler(_repository).Handle(new GetGraphSnapshotQuery("b1"), default));

        Assert.Equal("board.graph_not_published", exception.Code);
    }

    [Fact]
    public async Task Snapshot_OfUnknownBoard_ThrowsNotFound()
    {
        var exception = await Assert.ThrowsAsync<BoardNotFoundException>(
            () => new GetGraphSnapshotQueryHandler(_repository).Handle(new GetGraphSnapshotQuery("nope"), default));

        Assert.Equal("board.not_found", exception.Code);
    }
}
