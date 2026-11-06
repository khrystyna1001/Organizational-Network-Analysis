namespace ONA.Graph.Domain.Exceptions;

public sealed class InvalidBoardIdException : DomainException
{
    public string? AttemptedId { get; }

    public InvalidBoardIdException(string? attemptedId)
        : base("board.invalid_id", "Board ID cannot be empty or whitespace.")
    {
        AttemptedId = attemptedId;
    }
}
