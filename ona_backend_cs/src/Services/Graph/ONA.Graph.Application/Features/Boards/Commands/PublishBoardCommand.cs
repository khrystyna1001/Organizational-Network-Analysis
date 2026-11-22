using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>
/// UPDATE (domain command) – PublishGraphVersion from docs/architecture/stateDiagram-analysis.md.
/// Built -> Published. Saves a new GraphVersionId; the published graph becomes immutable.
/// Forbidden transitions throw InvalidGraphStateTransitionException and nothing is saved.
/// </summary>
public sealed record PublishBoardCommand(string BoardId) : ICommand<BoardDto>;

public sealed class PublishBoardCommandHandler(IBoardRepository repository)
    : ICommandHandler<PublishBoardCommand, BoardDto>
{
    public async Task<BoardDto> HandleAsync(
        PublishBoardCommand command,
        CancellationToken cancellationToken = default)
    {
        // Tracked load: the change made by the aggregate is persisted by SaveAsync.
        var board = await repository.GetByIdAsync(command.BoardId, cancellationToken)
            ?? throw new BoardNotFoundException(command.BoardId);

        board.PublishGraphVersion();

        await repository.SaveAsync(board, cancellationToken);
        return BoardDto.FromDomain(board);
    }
}
