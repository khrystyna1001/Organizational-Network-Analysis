using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Features.Boards.Commands;

/// <summary>
/// UPDATE (details): renames the board / changes its description through Board.UpdateDetails.
/// When <see cref="DescriptionSpecified"/> is false the current description is kept.
/// </summary>
public sealed record RenameBoardCommand(
    string BoardId,
    string? Name,
    string? Description,
    bool DescriptionSpecified) : IRequest<BoardDto>;

public sealed class RenameBoardCommandHandler(IBoardRepository repository)
    : IRequestHandler<RenameBoardCommand, BoardDto>
{
    public async Task<BoardDto> Handle(
        RenameBoardCommand command,
        CancellationToken cancellationToken = default)
    {
        var board = await repository.GetByIdAsync(command.BoardId, cancellationToken)
            ?? throw new BoardNotFoundException(command.BoardId);

        board.UpdateDetails(
            command.Name ?? board.Name,
            command.DescriptionSpecified ? command.Description : board.Description);

        await repository.SaveAsync(board, cancellationToken);
        return BoardDto.FromDomain(board);
    }
}
