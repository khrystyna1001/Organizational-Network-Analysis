using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>
/// UPDATE (domain command) – ArchiveBoard from docs/architecture/stateDiagram-analysis.md.
/// Published -> Archived (terminal state).
/// Forbidden transitions throw InvalidGraphStateTransitionException and nothing is saved.
/// </summary>
public sealed record ArchiveBoardCommand(string BoardId) : IRequest<BoardDto>;

public sealed class ArchiveBoardCommandHandler(IBoardRepository repository)
    : IRequestHandler<ArchiveBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(
        ArchiveBoardCommand command,
        CancellationToken cancellationToken = default)
    {
        // Tracked load: the change made by the aggregate is persisted by SaveAsync.
        var board = await repository.GetByIdAsync(command.BoardId, cancellationToken)
            ?? throw new BoardNotFoundException(command.BoardId);

        board.Archive();

        await repository.SaveAsync(board, cancellationToken);
        return BoardDto.FromDomain(board);
    }
}
