using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Nodes.Queries;

/// <summary>READ (single) – returns null when the node does not exist.</summary>
public sealed record GetNodeByIdQuery(string NodeId) : IRequest<NodeDto?>;

public sealed class GetNodeByIdQueryHandler(IGraphService service) : IRequestHandler<GetNodeByIdQuery, NodeDto?>
{
    public Task<NodeDto?> Handle(GetNodeByIdQuery query, CancellationToken cancellationToken = default) =>
        service.GetNodeAsync(query.NodeId, cancellationToken);
}
