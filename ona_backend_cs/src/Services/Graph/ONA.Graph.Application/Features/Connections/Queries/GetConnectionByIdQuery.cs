using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Connections.Queries;

/// <summary>READ (single) – returns null when the connection does not exist.</summary>
public sealed record GetConnectionByIdQuery(string ConnectionId) : IRequest<ConnectionDto?>;

public sealed class GetConnectionByIdQueryHandler(IGraphService service)
    : IRequestHandler<GetConnectionByIdQuery, ConnectionDto?>
{
    public Task<ConnectionDto?> Handle(GetConnectionByIdQuery query, CancellationToken cancellationToken = default) =>
        service.GetConnectionAsync(query.ConnectionId, cancellationToken);
}
