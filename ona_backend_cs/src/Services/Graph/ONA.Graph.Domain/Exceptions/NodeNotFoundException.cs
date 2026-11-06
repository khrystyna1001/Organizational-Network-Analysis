namespace ONA.Graph.Domain.Exceptions;

public sealed class NodeNotFoundException : DomainException
{
    public string NodeId { get; }
    public string BoardId { get; }

    public NodeNotFoundException(string nodeId, string boardId)
        : base("node.not_found", $"Node '{nodeId}' was not found in board '{boardId}'.")
    {
        NodeId = nodeId;
        BoardId = boardId;
    }
}
