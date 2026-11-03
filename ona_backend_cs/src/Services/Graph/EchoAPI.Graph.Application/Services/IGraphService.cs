using EchoAPI.Graph.Application.DTOs;

namespace EchoAPI.Graph.Application.Services;

public interface IGraphService
{
    Task<BoardDto> CreateBoardAsync(
        CreateBoardDto input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BoardDto>> GetBoardsAsync(
        CancellationToken cancellationToken = default);

    Task<BoardDto?> GetBoardAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<BoardDto?> UpdateBoardAsync(
        string id,
        UpdateBoardDto input,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteBoardAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<NodeDto> AddNodeAsync(
        string boardId,
        CreateNodeDto input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NodeDto>> GetNodesAsync(
        string? boardId = null,
        CancellationToken cancellationToken = default);

    Task<NodeDto?> GetNodeAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<NodeDto?> UpdateNodeAsync(
        string id,
        UpdateNodeDto input,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteNodeAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<ConnectionDto> AddConnectionAsync(
        string boardId,
        CreateConnectionDto input,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConnectionDto>> GetConnectionsAsync(
        string? boardId = null,
        CancellationToken cancellationToken = default);

    Task<ConnectionDto?> GetConnectionAsync(
        string id,
        CancellationToken cancellationToken = default);

    Task<ConnectionDto?> UpdateConnectionAsync(
        string id,
        UpdateConnectionDto input,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteConnectionAsync(
        string id,
        CancellationToken cancellationToken = default);
}
