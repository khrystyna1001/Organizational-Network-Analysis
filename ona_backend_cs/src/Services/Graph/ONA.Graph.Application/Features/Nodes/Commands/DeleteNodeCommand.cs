using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Nodes.Commands;

/// <summary>DELETE – removes a node and its connections. Returns false when the node does not exist.</summary>
public sealed record DeleteNodeCommand(string NodeId) : IRequest<bool>;

public sealed class DeleteNodeCommandHandler(IGraphService service) : IRequestHandler<DeleteNodeCommand, bool>
{
    public Task<bool> Handle(DeleteNodeCommand command, CancellationToken cancellationToken = default) =>
        service.DeleteNodeAsync(command.NodeId, cancellationToken);
}
