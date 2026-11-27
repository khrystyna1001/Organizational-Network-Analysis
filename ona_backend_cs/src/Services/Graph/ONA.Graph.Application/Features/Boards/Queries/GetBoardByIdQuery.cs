using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;

namespace ONA.Graph.Application.Features.Boards.Queries;

/// <summary>READ (single): no change tracking, returns a flat DTO or null.</summary>
public sealed record GetBoardByIdQuery(string BoardId) : IRequest<BoardDto?>;

public sealed class GetBoardByIdQueryHandler(IBoardRepository repository)
    : IRequestHandler<GetBoardByIdQuery, BoardDto?>
{
    public async Task<BoardDto?> Handle(
        GetBoardByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var board = await repository.GetByIdReadOnlyAsync(query.BoardId, cancellationToken);
        return board is null ? null : BoardDto.FromDomain(board);
    }
}
