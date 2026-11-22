using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Application.DTOs;

/// <summary>Board data returned by the Graph API.</summary>
public sealed record BoardDto(
    string Id,
    string Name,
    string? Description,
    string Lifecycle = nameof(BoardLifecycle.Draft),
    string? CurrentGraphVersionId = null,
    int NodeCount = 0,
    int ConnectionCount = 0)
{
    public static BoardDto FromDomain(Board board) =>
        new(
            board.Id,
            board.Name,
            board.Description,
            board.Lifecycle.ToString(),
            board.CurrentGraphVersionId,
            board.Nodes.Count,
            board.Connections.Count);
}
