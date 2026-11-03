using EchoAPI.Graph.Domain.Exceptions;

namespace EchoAPI.Graph.Domain.Entities;

public sealed class Connection
{
    public string Id { get; }
    public string BoardId { get; }
    public string SourceNodeId { get; }
    public string TargetNodeId { get; }
    public double? Weight { get; private set; }

    internal Connection(
        string? id,
        string? boardId,
        string? sourceNodeId,
        string? targetNodeId,
        double? weight)
    {
        Id = Require(id, "id");
        BoardId = Require(boardId, "board_id");
        SourceNodeId = Require(sourceNodeId, "source_node_id");
        TargetNodeId = Require(targetNodeId, "target_node_id");
        Weight = weight;
    }

    internal void UpdateWeight(double? weight) => Weight = weight;

    private static string Require(string? value, string fieldName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidConnectionException(fieldName, value);
}
