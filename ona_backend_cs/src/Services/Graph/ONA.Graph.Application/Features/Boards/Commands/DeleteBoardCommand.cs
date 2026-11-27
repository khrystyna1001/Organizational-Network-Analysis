using ONA.Graph.Application.Abstractions;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>
/// DELETE with guard: a Published board cannot be deleted (archive it first).
/// Returns false when the board does not exist.
/// </summary>
public sealed record DeleteBoardCommand(string BoardId) : IRequest<bool>;

public sealed class DeleteBoardCommandHandler(IBoardRepository repository)
    : IRequestHandler<DeleteBoardCommand, bool>
{
    public async Task<bool> Handle(
        DeleteBoardCommand command,
        CancellationToken cancellationToken = default)
    {
        var board = await repository.GetByIdAsync(command.BoardId, cancellationToken);
        if (board is null)
        {
            return false;
        }

        // Domain rule: throws BoardDeletionNotAllowedException for a Published board.
        board.EnsureCanBeDeleted();

        await repository.DeleteAsync(board, cancellationToken);
        return true;
    }
}
