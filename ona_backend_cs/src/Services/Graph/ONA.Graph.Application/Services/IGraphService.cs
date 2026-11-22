using ONA.Graph.Application.DTOs;

namespace ONA.Graph.Application.Services;

public interface IGraphService
{
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
