using ONA.Graph.Domain.Entities;
using ONA.Graph.Domain.Exceptions;
using ONA.Graph.Domain.ValueObjects;

namespace ONA.Graph.Domain.Aggregates;

public sealed class Board
{
    private readonly object _sync = new();
    private readonly List<Node> _nodes = [];
    private readonly List<Connection> _connections = [];

    public string Id { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public BoardLifecycle Lifecycle { get; private set; } = BoardLifecycle.Draft;
    public string? CurrentGraphVersionId { get; private set; }

    private IBoardState State => BoardStates.For(Lifecycle);

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

        Id = id;
        Name = new BoardName(name).Value;
        Description = description;
    }

    public void UpdateDetails(string? name, string? description)
    {
        var boardName = new BoardName(name);

        lock (_sync)
        {
            Name = boardName.Value;
            Description = description;
        }
    }

    /// <summary>Draft -> Built. Requires at least one node (stateDiagram-analysis.md).</summary>
    public void BuildNetworkGraph()
    {
        lock (_sync)
        {
            Lifecycle = State.Build(this);
        }
    }

    /// <summary>Built -> Published. Saves a new immutable GraphVersion identifier.</summary>
    public string PublishGraphVersion()
    {
        lock (_sync)
        {
            var next = State.Publish(this);
            CurrentGraphVersionId = Guid.NewGuid().ToString();
            Lifecycle = next;
            return CurrentGraphVersionId;
        }
    }

    /// <summary>Published -> Archived (terminal state).</summary>
    public void Archive()
    {
        lock (_sync)
        {
            Lifecycle = State.Archive(this);
        }
    }

    /// <summary>
    /// Delete guard: a Published board is in use by analysis consumers and must be archived
    /// before it can be removed.
    /// </summary>
    public void EnsureCanBeDeleted()
    {
        if (!State.AllowsDeletion)
        {
            throw new BoardDeletionNotAllowedException(Id, Lifecycle);
        }
    }

    public Node AddNode(NodeName name, string? title, DepartmentName department)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(department);
        return AddNode(name.Value, title, department.Value);
    }

    public Node AddNode(string? name, string? title, string? department)
    {
        lock (_sync)
        {
            EnsureGraphIsEditable();
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
            EnsureGraphIsEditable();
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
            EnsureGraphIsEditable();
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
            EnsureGraphIsEditable();
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
            EnsureGraphIsEditable();
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
            EnsureGraphIsEditable();
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

    private void EnsureGraphIsEditable()
    {
        if (!State.AllowsGraphMutation)
        {
            throw new PublishedGraphMutationNotAllowedException(Id, Lifecycle);
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
