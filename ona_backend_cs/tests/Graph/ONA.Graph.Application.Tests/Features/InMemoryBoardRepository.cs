using ONA.Graph.Application.Abstractions;
using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Application.Tests.Features;

/// <summary>
/// Hand-written fake (state-based testing). Handlers are tested against real Board aggregates,
/// so removing a domain guard makes these tests fail – unlike a configured Moq return value.
/// </summary>
internal sealed class InMemoryBoardRepository : IBoardRepository
{
    private readonly Dictionary<string, Board> _boards = new(StringComparer.Ordinal);

    public int SaveCount { get; private set; }

    public IReadOnlyCollection<Board> Boards => _boards.Values;

    public Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Board>>(_boards.Values.ToArray());

    public Task<Board?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_boards.GetValueOrDefault(id));

    public Task<Board?> GetByIdReadOnlyAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_boards.GetValueOrDefault(id));

    public Task AddAsync(Board board, CancellationToken cancellationToken = default)
    {
        _boards.Add(board.Id, board);
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task SaveAsync(Board board, CancellationToken cancellationToken = default)
    {
        _boards[board.Id] = board;
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Board board, CancellationToken cancellationToken = default)
    {
        _boards.Remove(board.Id);
        SaveCount++;
        return Task.CompletedTask;
    }

    public Board Seed(Board board)
    {
        _boards.Add(board.Id, board);
        return board;
    }
}
