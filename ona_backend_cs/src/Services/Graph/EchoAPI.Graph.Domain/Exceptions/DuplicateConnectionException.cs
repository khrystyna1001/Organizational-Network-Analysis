namespace EchoAPI.Graph.Domain.Exceptions;

public sealed class DuplicateConnectionException : DomainException
{
    public string SourceNodeId { get; }
    public string TargetNodeId { get; }

    public DuplicateConnectionException(string sourceNodeId, string targetNodeId)
        : base(
            "connection.duplicate",
            $"A connection from node '{sourceNodeId}' to node '{targetNodeId}' already exists.")
    {
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
    }
}
