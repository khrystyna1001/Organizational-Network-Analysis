using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Nodes.Queries;

/// <summary>READ (list) – nodes of one board, or of all boards when <paramref name="BoardId"/> is empty.</summary>
public sealed record GetNodesQuery(string? BoardId) : IRequest<IReadOnlyList<NodeDto>>;

public sealed class GetNodesQueryHandler(IGraphService service)
    : IRequestHandler<GetNodesQuery, IReadOnlyList<NodeDto>>
{
    public Task<IReadOnlyList<NodeDto>> Handle(GetNodesQuery query, CancellationToken cancellationToken = default) =>
        service.GetNodesAsync(query.BoardId, cancellationToken);
}
