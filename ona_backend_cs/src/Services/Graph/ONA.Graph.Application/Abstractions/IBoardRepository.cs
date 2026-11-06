using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Application.Abstractions;

public interface IBoardRepository
{
    Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Board?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task SaveAsync(Board board, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
