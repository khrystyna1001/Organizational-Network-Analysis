using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Events;
using ONA.Graph.Application.Ports;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>
/// UPDATE (domain command) – PublishGraphVersion from docs/architecture/stateDiagram-analysis.md.
/// Built -> Published. Saves a new GraphVersionId; the published graph becomes immutable.
/// Forbidden transitions throw InvalidGraphStateTransitionException and nothing is saved.
/// </summary>
public sealed record PublishBoardCommand(string BoardId) : IRequest<BoardDto>;

public sealed class PublishBoardCommandHandler(
    IBoardRepository repository,
    IGraphEventPublisher eventPublisher)
    : IRequestHandler<PublishBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(
        PublishBoardCommand command,
        CancellationToken cancellationToken = default)
    {
        // Tracked load: the change made by the aggregate is persisted by SaveAsync.
        var board = await repository.GetByIdAsync(command.BoardId, cancellationToken)
            ?? throw new BoardNotFoundException(command.BoardId);

        board.PublishGraphVersion();

        await repository.SaveAsync(board, cancellationToken);

        // At-least-once, no outbox: the event is sent after the commit. A broker failure must not
        // fail the request or undo the publication (the Kafka adapter logs the failure).
        try
        {
            await eventPublisher.PublishGraphVersionPublishedAsync(
                new GraphVersionPublished(
                    Guid.NewGuid().ToString(),
                    DateTimeOffset.UtcNow,
                    board.Id,
                    board.CurrentGraphVersionId!),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Swallowed on purpose (see above).
        }

        return BoardDto.FromDomain(board);
    }
}
