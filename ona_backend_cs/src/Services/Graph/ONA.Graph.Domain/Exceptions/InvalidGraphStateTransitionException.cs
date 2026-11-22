using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Domain.Exceptions;

public sealed class InvalidGraphStateTransitionException : DomainException
{
    public string BoardId { get; }
    public BoardLifecycle CurrentLifecycle { get; }
    public string Command { get; }

    public InvalidGraphStateTransitionException(
        string boardId,
        BoardLifecycle currentLifecycle,
        string command)
        : base(
            "board.invalid_state_transition",
            $"Command '{command}' is not allowed for board '{boardId}' in state '{currentLifecycle}'.")
    {
        BoardId = boardId;
        CurrentLifecycle = currentLifecycle;
        Command = command;
    }
}
