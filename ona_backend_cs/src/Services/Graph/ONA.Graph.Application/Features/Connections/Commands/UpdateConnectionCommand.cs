using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Connections.Commands;

/// <summary>UPDATE – changes the weight of a connection. Returns null when it does not exist.</summary>
public sealed record UpdateConnectionCommand(string ConnectionId, UpdateConnectionDto Input) : IRequest<ConnectionDto?>;

public sealed class UpdateConnectionCommandHandler(IGraphService service)
    : IRequestHandler<UpdateConnectionCommand, ConnectionDto?>
{
    public Task<ConnectionDto?> Handle(UpdateConnectionCommand command, CancellationToken cancellationToken = default) =>
        service.UpdateConnectionAsync(command.ConnectionId, command.Input, cancellationToken);
}
