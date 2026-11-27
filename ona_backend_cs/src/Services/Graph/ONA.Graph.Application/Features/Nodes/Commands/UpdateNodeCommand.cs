using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Nodes.Commands;

/// <summary>UPDATE – changes node details. Returns null when the node does not exist.</summary>
public sealed record UpdateNodeCommand(string NodeId, UpdateNodeDto Input) : IRequest<NodeDto?>;

public sealed class UpdateNodeCommandHandler(IGraphService service) : IRequestHandler<UpdateNodeCommand, NodeDto?>
{
    public Task<NodeDto?> Handle(UpdateNodeCommand command, CancellationToken cancellationToken = default) =>
        service.UpdateNodeAsync(command.NodeId, command.Input, cancellationToken);
}
