namespace ONA.Analysis.Application.Ports;

public sealed record SnapshotNode(string Id, string Name);

public sealed record SnapshotConnection(string SourceNodeId, string TargetNodeId, double Weight);

/// <summary>Immutable view of a published Graph version, in Analysis' own terms.</summary>
public sealed record GraphSnapshot(
    string BoardId,
    string GraphVersionId,
    IReadOnlyList<SnapshotNode> Nodes,
    IReadOnlyList<SnapshotConnection> Connections);

/// <summary>
/// Reads the published graph of a board from the Graph context.
/// Business failures throw DomainException (board missing / not published);
/// technical failures throw <see cref="GraphIntegrationException"/>.
/// </summary>
public interface IGraphSnapshotPort
{
    Task<GraphSnapshot> GetSnapshotAsync(string boardId, CancellationToken cancellationToken = default);
}

public sealed class GraphIntegrationException : Exception
{
    public GraphIntegrationException(string message) : base(message)
    {
    }

    public GraphIntegrationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
