namespace EchoAPI.Graph.Domain.Exceptions;

public sealed class BoardNotFoundException : DomainException
{
    public string BoardId { get; }

    public BoardNotFoundException(string boardId)
        : base("board.not_found", $"Board '{boardId}' was not found.")
    {
        BoardId = boardId;
    }
}
