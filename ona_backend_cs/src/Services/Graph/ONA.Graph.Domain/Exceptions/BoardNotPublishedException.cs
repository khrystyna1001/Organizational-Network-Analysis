namespace ONA.Graph.Domain.Exceptions;

/// <summary>A snapshot exists only for a board that has a saved GraphVersion (Published or Archived).</summary>
public sealed class BoardNotPublishedException : DomainException
{
    public string BoardId { get; }

    public BoardNotPublishedException(string boardId)
        : base(
            "board.graph_not_published",
            $"Board '{boardId}' has no published graph version.")
    {
        BoardId = boardId;
    }
}
