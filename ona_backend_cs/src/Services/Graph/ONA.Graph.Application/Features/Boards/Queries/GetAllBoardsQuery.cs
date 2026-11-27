using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;

namespace ONA.Graph.Application.Features.Boards.Queries;

/// <summary>READ (list): GetAllAsync runs with AsNoTracking() in MySqlBoardRepository.</summary>
public sealed record GetAllBoardsQuery : IRequest<IReadOnlyList<BoardDto>>;

public sealed class GetAllBoardsQueryHandler(IBoardRepository repository)
    : IRequestHandler<GetAllBoardsQuery, IReadOnlyList<BoardDto>>
{
    public async Task<IReadOnlyList<BoardDto>> Handle(
        GetAllBoardsQuery query,
        CancellationToken cancellationToken = default)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards.Select(BoardDto.FromDomain).ToArray();
    }
}
