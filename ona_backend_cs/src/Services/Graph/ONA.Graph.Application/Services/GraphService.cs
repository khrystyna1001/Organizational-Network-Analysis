using ONA.Graph.Application.Abstractions;
using ONA.Graph.Application.DTOs;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Entities;
using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Application.Services;

/// <summary>
/// Node and connection management inside a Board aggregate.
/// Board lifecycle CRUD lives in Features/Boards (CQRS commands and queries).
/// </summary>
public sealed class GraphService(IBoardRepository repository) : IGraphService
{
    public async Task<NodeDto> AddNodeAsync(
        string boardId,
        CreateNodeDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await GetRequiredBoardAsync(boardId, cancellationToken);
        var node = board.AddNode(input.Name, input.Title, input.Department);
        await repository.SaveAsync(board, cancellationToken);
        return ToDto(node);
    }

    public async Task<IReadOnlyList<NodeDto>> GetNodesAsync(
        string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards
            .Where(board => string.IsNullOrEmpty(boardId) || board.Id == boardId)
            .SelectMany(board => board.Nodes.Select(ToDto))
            .ToArray();
    }

    public async Task<NodeDto?> GetNodeAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingNodeAsync(id, cancellationToken);
        var node = board?.FindNode(id);
        return node is null ? null : ToDto(node);
    }

    public async Task<NodeDto?> UpdateNodeAsync(
        string id,
        UpdateNodeDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingNodeAsync(id, cancellationToken);
        var node = board?.FindNode(id);
        if (board is null || node is null)
        {
            return null;
        }

        if (!board.UpdateNode(
                id,
                input.Name ?? node.Name,
                input.Title ?? node.Title,
                input.Department ?? node.Department))
        {
            return null;
        }

        await repository.SaveAsync(board, cancellationToken);
        return board.FindNode(id) is { } updated ? ToDto(updated) : null;
    }

    public async Task<bool> DeleteNodeAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingNodeAsync(id, cancellationToken);
        if (board is null || !board.RemoveNode(id))
        {
            return false;
        }

        await repository.SaveAsync(board, cancellationToken);
        return true;
    }

    public async Task<ConnectionDto> AddConnectionAsync(
        string boardId,
        CreateConnectionDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await GetRequiredBoardAsync(boardId, cancellationToken);
        var connection = board.AddConnection(
            input.SourceNodeId,
            input.TargetNodeId,
            input.Weight);
        await repository.SaveAsync(board, cancellationToken);
        return ToDto(connection);
    }

    public async Task<IReadOnlyList<ConnectionDto>> GetConnectionsAsync(
        string? boardId = null,
        CancellationToken cancellationToken = default)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards
            .Where(board => string.IsNullOrEmpty(boardId) || board.Id == boardId)
            .SelectMany(board => board.Connections.Select(ToDto))
            .ToArray();
    }

    public async Task<ConnectionDto?> GetConnectionAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingConnectionAsync(id, cancellationToken);
        var connection = board?.FindConnection(id);
        return connection is null ? null : ToDto(connection);
    }

    public async Task<ConnectionDto?> UpdateConnectionAsync(
        string id,
        UpdateConnectionDto input,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingConnectionAsync(id, cancellationToken);
        var connection = board?.FindConnection(id);
        if (board is null || connection is null)
        {
            return null;
        }

        var weight = input.WeightSpecified ? input.Weight : connection.Weight;
        if (!board.UpdateConnectionWeight(id, weight))
        {
            return null;
        }

        await repository.SaveAsync(board, cancellationToken);
        return board.FindConnection(id) is { } updated ? ToDto(updated) : null;
    }

    public async Task<bool> DeleteConnectionAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await FindBoardContainingConnectionAsync(id, cancellationToken);
        if (board is null || !board.RemoveConnection(id))
        {
            return false;
        }

        await repository.SaveAsync(board, cancellationToken);
        return true;
    }

    private async Task<Board> GetRequiredBoardAsync(
        string id,
        CancellationToken cancellationToken) =>
        await repository.GetByIdAsync(id, cancellationToken)
        ?? throw new BoardNotFoundException(id);

    private async Task<Board?> FindBoardContainingNodeAsync(
        string nodeId,
        CancellationToken cancellationToken)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards.FirstOrDefault(board => board.FindNode(nodeId) is not null);
    }

    private async Task<Board?> FindBoardContainingConnectionAsync(
        string connectionId,
        CancellationToken cancellationToken)
    {
        var boards = await repository.GetAllAsync(cancellationToken);
        return boards.FirstOrDefault(board => board.FindConnection(connectionId) is not null);
    }

    private static NodeDto ToDto(Node node) =>
        new(node.Id, node.BoardId, node.Name, node.Title, node.Department);

    private static ConnectionDto ToDto(Connection connection) =>
        new(
            connection.Id,
            connection.BoardId,
            connection.SourceNodeId,
            connection.TargetNodeId,
            connection.Weight);
}
