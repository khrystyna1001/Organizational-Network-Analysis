using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Connections.Commands;

/// <summary>DELETE – removes a connection. Returns false when it does not exist.</summary>
public sealed record DeleteConnectionCommand(string ConnectionId) : IRequest<bool>;

public sealed class DeleteConnectionCommandHandler(IGraphService service)
    : IRequestHandler<DeleteConnectionCommand, bool>
{
    public Task<bool> Handle(DeleteConnectionCommand command, CancellationToken cancellationToken = default) =>
        service.DeleteConnectionAsync(command.ConnectionId, cancellationToken);
}
