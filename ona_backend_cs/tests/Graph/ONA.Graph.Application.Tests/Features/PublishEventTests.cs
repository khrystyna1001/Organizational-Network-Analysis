using ONA.Graph.Application.Events;
using ONA.Graph.Application.Features.Boards.Commands;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Tests.Features;

public sealed class PublishEventTests
{
    private readonly InMemoryBoardRepository _repository = new();

    private Board SeedBuiltBoard()
    {
        var board = _repository.Seed(new Board("b1", "Engineering", null));
        board.AddNode("Alex", "Director", "Engineering");
        board.BuildNetworkGraph();
        return board;
    }

    [Fact]
    public async Task Publish_PublishesExactlyOneEventWithTheNewVersionId()
    {
        var board = SeedBuiltBoard();
        var publisher = new RecordingEventPublisher();

        var result = await new PublishBoardCommandHandler(_repository, publisher)
            .Handle(new PublishBoardCommand("b1"), default);

        var published = Assert.Single(publisher.Events);
        Assert.Equal("b1", published.BoardId);
        Assert.Equal(board.CurrentGraphVersionId, published.GraphVersionId);
        Assert.Equal(result.CurrentGraphVersionId, published.GraphVersionId);
        Assert.False(string.IsNullOrWhiteSpace(published.EventId));
        Assert.Equal("ona.graph.version-published", GraphVersionPublished.Topic);
    }

    [Fact]
    public async Task Publish_WhenBrokerFails_StillSucceedsAndBoardStaysPublished()
    {
        var board = SeedBuiltBoard();
        var publisher = new RecordingEventPublisher { ThrowOnPublish = true };

        var result = await new PublishBoardCommandHandler(_repository, publisher)
            .Handle(new PublishBoardCommand("b1"), default);

        Assert.Equal(nameof(BoardLifecycle.Published), result.Lifecycle);
        Assert.Equal(BoardLifecycle.Published, board.Lifecycle);
        Assert.NotNull(board.CurrentGraphVersionId);
    }

    [Fact]
    public async Task Publish_FromInvalidState_PublishesNoEvent()
    {
        _repository.Seed(new Board("b1", "Engineering", null)).AddNode("Alex", "Director", "Engineering");
        var publisher = new RecordingEventPublisher();

        var exception = await Assert.ThrowsAsync<InvalidGraphStateTransitionException>(
            () => new PublishBoardCommandHandler(_repository, publisher)
                .Handle(new PublishBoardCommand("b1"), default));

        Assert.Equal("board.invalid_state_transition", exception.Code);
        Assert.Empty(publisher.Events);
    }
}
