using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;

namespace ONA.Graph.Application.Features.Boards.Queries;

/// <summary>READ (list): GetAllAsync runs with AsNoTracking() in MySqlBoardRepository.</summary>
public sealed record GetAllBoardsQuery : IQuery<IReadOnlyList<BoardDto>>;

public sealed class GetAllBoardsQueryHandler(IBoardRepository repository)
    : IQueryHandler<GetAllBoardsQuery, IReadOnlyList<BoardDto>>
{
    public async Task<IReadOnlyList<BoardDto>> HandleAsync(
        GetAllBoardsQuery query,
        CancellationToken cancellationToken = default)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards.Select(BoardDto.FromDomain).ToArray();
    }
}
