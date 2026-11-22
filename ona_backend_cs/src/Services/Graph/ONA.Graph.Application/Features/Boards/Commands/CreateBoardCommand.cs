using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>CREATE: CreateBoard (Draft) – docs/architecture/stateDiagram-analysis.md.</summary>
public sealed record CreateBoardCommand(string? Name, string? Description) : ICommand<BoardDto>;

public sealed class CreateBoardCommandHandler(IBoardRepository repository)
    : ICommandHandler<CreateBoardCommand, BoardDto>
{
    public async Task<BoardDto> HandleAsync(
        CreateBoardCommand command,
        CancellationToken cancellationToken = default)
    {
        // Board validates its BoardName value object (board.invalid_name) and starts in Draft.
        var board = new Board(Guid.NewGuid().ToString(), command.Name, command.Description);

        await repository.AddAsync(board, cancellationToken);

        return BoardDto.FromDomain(board);
    }
}
