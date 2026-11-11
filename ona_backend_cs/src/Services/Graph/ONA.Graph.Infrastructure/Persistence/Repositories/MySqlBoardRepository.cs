using Microsoft.EntityFrameworkCore;
using ONA.Graph.Application.Abstractions;
using ONA.Graph.Domain.Aggregates;

namespace ONA.Graph.Infrastructure.Persistence.Repositories;

internal sealed class MySqlBoardRepository(GraphDbContext dbContext) : IBoardRepository
{
    public async Task<IReadOnlyList<Board>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await WithGraph(dbContext.Boards)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public Task<Board?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        WithGraph(dbContext.Boards)
            .AsNoTracking()
            .SingleOrDefaultAsync(board => board.Id == id, cancellationToken);

    public async Task SaveAsync(
        Board board,
        CancellationToken cancellationToken = default)
    {
        if (dbContext.Entry(board).State == EntityState.Detached)
        {
            dbContext.Boards.Add(board);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await GetByIdAsync(id, cancellationToken);
        if (board is null)
        {
            return false;
        }

        dbContext.Boards.Remove(board);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IQueryable<Board> WithGraph(IQueryable<Board> boards) =>
        boards
            .Include(nameof(Board.Nodes))
            .Include(nameof(Board.Connections))
            .AsSplitQuery();
}
