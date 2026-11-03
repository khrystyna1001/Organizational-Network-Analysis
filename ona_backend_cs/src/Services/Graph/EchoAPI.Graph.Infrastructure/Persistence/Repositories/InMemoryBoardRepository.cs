using EchoAPI.Graph.Application.Abstractions;
using EchoAPI.Graph.Domain.Aggregates;

namespace EchoAPI.Graph.Infrastructure.Persistence.Repositories;

internal sealed class InMemoryBoardRepository(InMemoryGraphStore store) : IBoardRepository
{
    public Task<IReadOnlyList<Board>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Board>>(store.Boards.Values.ToArray());
    }

    public Task<Board?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.Boards.TryGetValue(id, out var board);
        return Task.FromResult(board);
    }

    public Task SaveAsync(Board board, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        store.Boards[board.Id] = board;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(store.Boards.TryRemove(id, out _));
    }
}
