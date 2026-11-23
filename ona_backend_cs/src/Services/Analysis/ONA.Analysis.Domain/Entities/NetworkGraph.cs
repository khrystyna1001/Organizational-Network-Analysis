using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Domain.Entities;

public sealed class NetworkNode
{
    public string Id { get; }
    public string Name { get; }

    public NetworkNode(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

public sealed class NetworkConnection
{
    public string SourceNodeId { get; }
    public string TargetNodeId { get; }
    public double Weight { get; }

    public NetworkConnection(string sourceNodeId, string targetNodeId, double weight)
    {
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        Weight = weight;
    }
}

/// <summary>The directed, weighted graph an analysis run reads. Validated on creation.</summary>
public sealed class NetworkGraph
{
    public IReadOnlyList<NetworkNode> Nodes { get; }
    public IReadOnlyList<NetworkConnection> Connections { get; }

    public NetworkGraph(IEnumerable<NetworkNode> nodes, IEnumerable<NetworkConnection> connections)
    {
        var nodeList = nodes.ToArray();
        var connectionList = connections.ToArray();

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodeList)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                throw new InvalidNetworkGraphException("a node has no id");
            }

            if (!ids.Add(node.Id))
            {
                throw new InvalidNetworkGraphException($"duplicate node id '{node.Id}'");
            }
        }

        var pairs = new HashSet<(string, string)>();
        foreach (var connection in connectionList)
        {
            if (!ids.Contains(connection.SourceNodeId) || !ids.Contains(connection.TargetNodeId))
            {
                throw new InvalidNetworkGraphException(
                    $"connection '{connection.SourceNodeId}' -> '{connection.TargetNodeId}' references an unknown node");
            }

            if (connection.SourceNodeId == connection.TargetNodeId)
            {
                throw new InvalidNetworkGraphException($"node '{connection.SourceNodeId}' is connected to itself");
            }

            if (double.IsNaN(connection.Weight) || double.IsInfinity(connection.Weight) || connection.Weight <= 0)
            {
                throw new InvalidNetworkGraphException("connection weights must be finite and greater than zero");
            }

            if (!pairs.Add((connection.SourceNodeId, connection.TargetNodeId)))
            {
                throw new InvalidNetworkGraphException(
                    $"duplicate connection '{connection.SourceNodeId}' -> '{connection.TargetNodeId}'");
            }
        }

        Nodes = nodeList;
        Connections = connectionList;
    }
}
