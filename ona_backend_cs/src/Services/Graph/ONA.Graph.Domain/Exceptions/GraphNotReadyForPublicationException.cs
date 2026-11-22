namespace ONA.Graph.Domain.Exceptions;

public sealed class GraphNotReadyForPublicationException : DomainException
{
    public string BoardId { get; }

    public GraphNotReadyForPublicationException(string boardId, string reason)
        : base(
            "board.graph_not_ready",
            $"Board '{boardId}' graph is not ready: {reason}")
    {
        BoardId = boardId;
    }
}
