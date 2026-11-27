using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Nodes.Commands;

/// <summary>CREATE – adds a node to a Draft board (the Board aggregate enforces its invariants).</summary>
public sealed record AddNodeCommand(string BoardId, CreateNodeDto Input) : IRequest<NodeDto>;

public sealed class AddNodeCommandHandler(IGraphService service) : IRequestHandler<AddNodeCommand, NodeDto>
{
    public Task<NodeDto> Handle(AddNodeCommand command, CancellationToken cancellationToken = default) =>
        service.AddNodeAsync(command.BoardId, command.Input, cancellationToken);
}
