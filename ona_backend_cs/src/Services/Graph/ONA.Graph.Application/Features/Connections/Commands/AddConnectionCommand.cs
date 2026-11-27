using ONA.Graph.Application.DTOs;
using ONA.Graph.Application.Services;

namespace ONA.Graph.Application.Features.Connections.Commands;

/// <summary>CREATE – adds a directed, weighted connection to a Draft board.</summary>
public sealed record AddConnectionCommand(string BoardId, CreateConnectionDto Input) : IRequest<ConnectionDto>;

public sealed class AddConnectionCommandHandler(IGraphService service)
    : IRequestHandler<AddConnectionCommand, ConnectionDto>
{
    public Task<ConnectionDto> Handle(AddConnectionCommand command, CancellationToken cancellationToken = default) =>
        service.AddConnectionAsync(command.BoardId, command.Input, cancellationToken);
}
