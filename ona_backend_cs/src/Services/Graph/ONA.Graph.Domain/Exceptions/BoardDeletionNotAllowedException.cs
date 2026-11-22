using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Domain.Exceptions;

public sealed class BoardDeletionNotAllowedException : DomainException
{
    public string BoardId { get; }
    public BoardLifecycle CurrentLifecycle { get; }

    public BoardDeletionNotAllowedException(string boardId, BoardLifecycle currentLifecycle)
        : base(
            "board.deletion_not_allowed",
            $"Board '{boardId}' is {currentLifecycle} and cannot be deleted; archive it first.")
    {
        BoardId = boardId;
        CurrentLifecycle = currentLifecycle;
    }
}
