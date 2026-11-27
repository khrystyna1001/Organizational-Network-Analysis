using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Connections.Queries;

/// <summary>READ (list) – connections of one board, or of all boards when <paramref name="BoardId"/> is empty.</summary>
public sealed record GetConnectionsQuery(string? BoardId) : IRequest<IReadOnlyList<ConnectionDto>>;

public sealed class GetConnectionsQueryHandler(IGraphService service)
    : IRequestHandler<GetConnectionsQuery, IReadOnlyList<ConnectionDto>>
{
    public Task<IReadOnlyList<ConnectionDto>> Handle(GetConnectionsQuery query, CancellationToken cancellationToken = default) =>
        service.GetConnectionsAsync(query.BoardId, cancellationToken);
}
