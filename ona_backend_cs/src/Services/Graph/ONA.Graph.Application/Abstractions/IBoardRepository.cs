using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Application.Abstractions;

public interface IBoardRepository
{
    /// <summary>Read-only list of boards with their graph (no change tracking).</summary>
    Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the aggregate for modification (change tracking enabled, children included).</summary>
    Task<Board?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Loads the aggregate for display only (no change tracking).</summary>
    Task<Board?> GetByIdReadOnlyAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Persists a newly created aggregate.</summary>
    Task AddAsync(Board board, CancellationToken cancellationToken = default);

    /// <summary>Persists changes of a new or already loaded aggregate.</summary>
    Task SaveAsync(Board board, CancellationToken cancellationToken = default);

    /// <summary>Removes a loaded aggregate (with its nodes and connections).</summary>
    Task DeleteAsync(Board board, CancellationToken cancellationToken = default);
}
