namespace EchoAPI.Graph.Domain.Exceptions;

public sealed class InvalidBoardException : DomainException
{
    public string? AttemptedName { get; }

    public InvalidBoardException(string? attemptedName)
        : base(
            code: "board.invalid_name",
            message: "Board name cannot be empty or whitespace.")
    {
        AttemptedName = attemptedName;
    }
}
