using EchoAPI.Graph.Domain.Aggregates;
using EchoAPI.Graph.Domain.Entities;
using EchoAPI.Graph.Domain.Exceptions;

namespace EchoAPI.Graph.Domain.Tests;

public sealed class BoardTests
{
    [Fact]
    public void BoardCanAddNodesAndConnections()
    {
        var board = new Board("board-id", "Engineering", null);

        var first = board.AddNode("Alex", "Director", "Engineering");
        var second = board.AddNode("Sam", "Manager", "Engineering");
        var connection = board.AddConnection(first.Id, second.Id, 1.5);

        Assert.Equal("Engineering", board.Name);
        Assert.Equal("Engineering", first.Department);
        Assert.Equal(2, board.Nodes.Count);
        Assert.Single(board.Connections);
        Assert.Equal(first.Id, connection.SourceNodeId);
        Assert.Equal(second.Id, connection.TargetNodeId);
        Assert.Equal(1.5, connection.Weight);
        Assert.IsAssignableFrom<IReadOnlyCollection<Node>>(board.Nodes);
    }

    [Fact]
    public void BoardUpdatesNodeThroughAggregateRoot()
    {
        var board = new Board("board-id", "Engineering", null);
        var node = board.AddNode("Alex", "Director", "Engineering");

        var updated = board.UpdateNode(
            node.Id,
            "Alex Smith",
            "Senior Director",
            "People");

        Assert.True(updated);
        Assert.Equal("Alex Smith", node.Name);
        Assert.Equal("Senior Director", node.Title);
        Assert.Equal("People", node.Department);
    }

    [Theory]
    [InlineData(null, "name")]
    [InlineData("", "name")]
    [InlineData(" ", "name")]
    [InlineData(" ", "title")]
    public void AddNodeRejectsMissingRequiredFields(string? invalidValue, string fieldName)
    {
        var board = new Board("board-id", "Engineering", null);

        var exception = Assert.Throws<InvalidNodeException>(() =>
            board.AddNode(
                fieldName == "name" ? invalidValue : "Alex",
                fieldName == "title" ? invalidValue : "Director",
                "Engineering"));

        Assert.Equal($"node.invalid_{fieldName}", exception.Code);
        Assert.Equal(fieldName, exception.FieldName);
        Assert.Equal(invalidValue, exception.AttemptedValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddConnectionRejectsNonPositiveWeights(double weight)
    {
        var board = CreateBoardWithTwoNodes(out var first, out var second);

        var exception = Assert.Throws<InvalidConnectionWeightException>(
            () => board.AddConnection(first.Id, second.Id, weight));

        Assert.Equal("connection.invalid_weight", exception.Code);
        Assert.Equal(weight, exception.AttemptedWeight);
        Assert.Empty(board.Connections);
    }

    [Fact]
    public void AddConnectionRejectsNodeIdsNotInTheBoard()
    {
        var board = new Board("board-id", "Engineering", null);

        var exception = Assert.Throws<NodeNotFoundException>(
            () => board.AddConnection("missing-source", "missing-target", 1.0));

        Assert.Equal("node.not_found", exception.Code);
        Assert.Equal("missing-source", exception.NodeId);
        Assert.Equal(board.Id, exception.BoardId);
        Assert.Empty(board.Connections);
    }

    [Fact]
    public void AddConnectionRejectsDuplicateDirectedLinks()
    {
        var board = CreateBoardWithTwoNodes(out var first, out var second);
        board.AddConnection(first.Id, second.Id, 1.0);

        var exception = Assert.Throws<DuplicateConnectionException>(
            () => board.AddConnection(first.Id, second.Id, 2.0));

        Assert.Equal("connection.duplicate", exception.Code);
        Assert.Equal(first.Id, exception.SourceNodeId);
        Assert.Equal(second.Id, exception.TargetNodeId);
        Assert.Single(board.Connections);
    }

    [Fact]
    public void RemovingNodeAlsoRemovesItsConnections()
    {
        var board = CreateBoardWithTwoNodes(out var first, out var second);
        board.AddConnection(first.Id, second.Id, 1.0);

        Assert.True(board.RemoveNode(first.Id));
        Assert.Single(board.Nodes);
        Assert.Empty(board.Connections);
    }

    private static Board CreateBoardWithTwoNodes(out Node first, out Node second)
    {
        var board = new Board("board-id", "Engineering", null);
        first = board.AddNode("Alex", "Director", "Engineering");
        second = board.AddNode("Sam", "Manager", "Engineering");
        return board;
    }
}
