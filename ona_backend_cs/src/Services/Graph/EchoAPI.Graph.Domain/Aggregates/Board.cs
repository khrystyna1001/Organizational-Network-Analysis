using EchoAPI.Graph.Domain.Entities;
using EchoAPI.Graph.Domain.Exceptions;

namespace EchoAPI.Graph.Domain.Aggregates;

public sealed class Board
{
    private readonly object _sync = new();
    private readonly List<Node> _nodes = [];
    private readonly List<Connection> _connections = [];

    public string Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    public IReadOnlyCollection<Node> Nodes
    {
        get
        {
            lock (_sync)
            {
                return _nodes.ToArray();
            }
        }
    }

    public IReadOnlyCollection<Connection> Connections
    {
        get
        {
            lock (_sync)
            {
                return _connections.ToArray();
            }
        }
    }

    public Board(string id, string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidBoardIdException(id);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidBoardException(name);
        }

        Id = id;
        Name = name;
        Description = description;
    }

    public void UpdateDetails(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidBoardException(name);
        }

        lock (_sync)
        {
            Name = name;
            Description = description;
        }
    }

    public Node AddNode(string? name, string? title, string? department)
    {
        lock (_sync)
        {
            var node = new Node(Guid.NewGuid().ToString(), Id, name, title, department);
            _nodes.Add(node);
            return node;
        }
    }

    public bool UpdateNode(
        string nodeId,
        string? name,
        string? title,
        string? department)
    {
        lock (_sync)
        {
            var node = _nodes.FirstOrDefault(item => item.Id == nodeId);
            if (node is null)
            {
                return false;
            }

            node.Update(name, title, department);
            return true;
        }
    }

    public bool RemoveNode(string nodeId)
    {
        lock (_sync)
        {
            var removed = _nodes.RemoveAll(node => node.Id == nodeId) > 0;
            if (removed)
            {
                _connections.RemoveAll(connection =>
                    connection.SourceNodeId == nodeId ||
                    connection.TargetNodeId == nodeId);
            }

            return removed;
        }
    }

    public Node? FindNode(string nodeId)
    {
        lock (_sync)
        {
            return _nodes.FirstOrDefault(node => node.Id == nodeId);
        }
    }

    public Connection AddConnection(
        string? sourceNodeId,
        string? targetNodeId,
        double? weight)
    {
        lock (_sync)
        {
            ValidateWeight(weight);
            RequireNode(sourceNodeId);
            RequireNode(targetNodeId);

            if (sourceNodeId == targetNodeId)
            {
                throw new InvalidConnectionException("target_node_id", targetNodeId);
            }

            if (_connections.Any(connection =>
                    connection.SourceNodeId == sourceNodeId &&
                    connection.TargetNodeId == targetNodeId))
            {
                throw new DuplicateConnectionException(sourceNodeId!, targetNodeId!);
            }

            var connection = new Connection(
                Guid.NewGuid().ToString(),
                Id,
                sourceNodeId,
                targetNodeId,
                weight);
            _connections.Add(connection);
            return connection;
        }
    }

    public bool UpdateConnectionWeight(string connectionId, double? weight)
    {
        lock (_sync)
        {
            var connection = _connections.FirstOrDefault(item => item.Id == connectionId);
            if (connection is null)
            {
                return false;
            }

            ValidateWeight(weight);
            connection.UpdateWeight(weight);
            return true;
        }
    }

    public bool RemoveConnection(string connectionId)
    {
        lock (_sync)
        {
            return _connections.RemoveAll(connection => connection.Id == connectionId) > 0;
        }
    }

    public Connection? FindConnection(string connectionId)
    {
        lock (_sync)
        {
            return _connections.FirstOrDefault(connection => connection.Id == connectionId);
        }
    }

    private void RequireNode(string? nodeId)
    {
        if (nodeId is null || !_nodes.Any(node => node.Id == nodeId))
        {
            throw new NodeNotFoundException(nodeId ?? string.Empty, Id);
        }
    }

    private static void ValidateWeight(double? weight)
    {
        if (weight.HasValue && (!double.IsFinite(weight.Value) || weight.Value <= 0))
        {
            throw new InvalidConnectionWeightException(weight.Value);
        }
    }
}
