using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.Abstractions.Messaging;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Features.Boards.Queries;

/// <summary>READ: the graph of the board's current GraphVersion, for the Analysis context.</summary>
public sealed record GetGraphSnapshotQuery(string BoardId) : IQuery<GraphSnapshotDto>;

public sealed class GetGraphSnapshotQueryHandler(IBoardRepository repository)
    : IQueryHandler<GetGraphSnapshotQuery, GraphSnapshotDto>
{
    public async Task<GraphSnapshotDto> HandleAsync(
        GetGraphSnapshotQuery query,
        CancellationToken cancellationToken = default)
    {
        var board = await repository.GetByIdReadOnlyAsync(query.BoardId, cancellationToken)
            ?? throw new BoardNotFoundException(query.BoardId);

        if (board.CurrentGraphVersionId is null)
        {
            throw new BoardNotPublishedException(board.Id);
        }

        return new GraphSnapshotDto(
            board.Id,
            board.CurrentGraphVersionId,
            board.Lifecycle.ToString(),
            board.Nodes
                .Select(n => new NodeDto(n.Id, n.BoardId, n.Name, n.Title, n.Department))
                .ToArray(),
            board.Connections
                .Select(c => new ConnectionDto(c.Id, c.BoardId, c.SourceNodeId, c.TargetNodeId, c.Weight))
                .ToArray());
    }
}
