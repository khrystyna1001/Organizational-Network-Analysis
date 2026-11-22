using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Domain.Tests;

/// <summary>
/// Board lifecycle (docs/architecture/stateDiagram-analysis.md) tested on real aggregates, no mocks.
/// </summary>
public sealed class BoardLifecycleTests
{
    [Fact]
    public void NewBoard_ShouldStartInDraftWithoutGraphVersion()
    {
        var board = new Board("board-id", "Engineering", null);

        Assert.Equal(BoardLifecycle.Draft, board.Lifecycle);
        Assert.Null(board.CurrentGraphVersionId);
    }

    [Fact]
    public void BuildNetworkGraph_WhenBoardHasNoNodes_ShouldThrowAndStayDraft()
    {
        var board = new Board("board-id", "Engineering", null);

        var exception = Assert.Throws<GraphNotReadyForPublicationException>(board.BuildNetworkGraph);

        Assert.Equal("board.graph_not_ready", exception.Code);
        Assert.Equal(BoardLifecycle.Draft, board.Lifecycle);
    }

    [Fact]
    public void PublishGraphVersion_FromBuilt_ShouldPublishAndSaveVersionId()
    {
        // Given a Draft board with a valid graph (scenario 1 in stateDiagram-analysis.md)
        var board = CreateBoardWithOneNode();

        // When
        board.BuildNetworkGraph();
        var versionId = board.PublishGraphVersion();

        // Then
        Assert.Equal(BoardLifecycle.Published, board.Lifecycle);
        Assert.False(string.IsNullOrWhiteSpace(versionId));
        Assert.Equal(versionId, board.CurrentGraphVersionId);
    }

    [Fact]
    public void PublishGraphVersion_FromDraft_ShouldThrowInvalidTransitionAndKeepState()
    {
        var board = CreateBoardWithOneNode();

        var exception = Assert.Throws<InvalidGraphStateTransitionException>(() => board.PublishGraphVersion());

        Assert.Equal("board.invalid_state_transition", exception.Code);
        Assert.Equal(BoardLifecycle.Draft, exception.CurrentLifecycle);
        Assert.Equal(BoardLifecycle.Draft, board.Lifecycle);
        Assert.Null(board.CurrentGraphVersionId);
    }

    [Fact]
    public void AddNode_WhenBoardIsPublished_ShouldThrowAndKeepGraphUnchanged()
    {
        // Scenario 4: reject direct mutation of a published graph
        var board = CreatePublishedBoard();

        var exception = Assert.Throws<PublishedGraphMutationNotAllowedException>(
            () => board.AddNode("Sam", "Manager", "Engineering"));

        Assert.Equal("board.published_graph_mutation_not_allowed", exception.Code);
        Assert.Single(board.Nodes);
    }

    [Fact]
    public void RemoveNode_WhenBoardIsPublished_ShouldThrowAndKeepNode()
    {
        var board = CreatePublishedBoard();
        var nodeId = board.Nodes.Single().Id;

        Assert.Throws<PublishedGraphMutationNotAllowedException>(() => board.RemoveNode(nodeId));
        Assert.Single(board.Nodes);
    }

    [Fact]
    public void EnsureCanBeDeleted_WhenPublished_ShouldThrowBoardDeletionNotAllowed()
    {
        var board = CreatePublishedBoard();

        var exception = Assert.Throws<BoardDeletionNotAllowedException>(board.EnsureCanBeDeleted);

        Assert.Equal("board.deletion_not_allowed", exception.Code);
    }

    [Fact]
    public void Archive_FromPublished_ShouldAllowDeletionAndForbidFurtherTransitions()
    {
        var board = CreatePublishedBoard();

        board.Archive();

        Assert.Equal(BoardLifecycle.Archived, board.Lifecycle);
        board.EnsureCanBeDeleted();
        Assert.Throws<InvalidGraphStateTransitionException>(board.BuildNetworkGraph);
        Assert.Throws<InvalidGraphStateTransitionException>(board.Archive);
    }

    [Fact]
    public void Archive_FromDraft_ShouldThrowInvalidTransition()
    {
        var board = new Board("board-id", "Engineering", null);

        Assert.Throws<InvalidGraphStateTransitionException>(board.Archive);
        Assert.Equal(BoardLifecycle.Draft, board.Lifecycle);
    }

    [Fact]
    public void EnsureCanBeDeleted_WhenDraft_ShouldNotThrow()
    {
        var board = new Board("board-id", "Engineering", null);

        board.EnsureCanBeDeleted();

        Assert.Equal(BoardLifecycle.Draft, board.Lifecycle);
    }

    private static Board CreateBoardWithOneNode()
    {
        var board = new Board("board-id", "Engineering", null);
        board.AddNode("Alex", "Director", "Engineering");
        return board;
    }

    private static Board CreatePublishedBoard()
    {
        var board = CreateBoardWithOneNode();
        board.BuildNetworkGraph();
        board.PublishGraphVersion();
        return board;
    }
}
