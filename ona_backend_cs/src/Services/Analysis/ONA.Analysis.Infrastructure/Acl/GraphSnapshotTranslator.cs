using ONA.Analysis.Application.Ports;
using ONA.Analysis.Domain.Exceptions;
using ONA.Analysis.Infrastructure.Acl.ExternalContracts;

namespace ONA.Analysis.Infrastructure.Acl;

/// <summary>Pure mapping from the Graph snapshot model to ours (no I/O).</summary>
internal sealed class GraphSnapshotTranslator
{
    public GraphSnapshot ToSnapshot(GRAPH_SNAPSHOT_DTO dto, string requestedBoardId)
    {
        if (string.IsNullOrWhiteSpace(dto.GraphVersionId))
        {
            throw new GraphNotPublishedException(requestedBoardId);
        }

        if (dto.Nodes is null || dto.Connections is null || string.IsNullOrWhiteSpace(dto.BoardId))
        {
            throw new GraphIntegrationException("The Graph snapshot is incomplete.");
        }

        var nodes = dto.Nodes.Select(n =>
            new SnapshotNode(n.Id ?? string.Empty, string.IsNullOrWhiteSpace(n.Name) ? n.Id ?? string.Empty : n.Name)).ToArray();
        var connections = dto.Connections.Select(c =>
            new SnapshotConnection(c.SourceNodeId ?? string.Empty, c.TargetNodeId ?? string.Empty, c.Weight)).ToArray();

        return new GraphSnapshot(dto.BoardId, dto.GraphVersionId, nodes, connections);
    }
}
