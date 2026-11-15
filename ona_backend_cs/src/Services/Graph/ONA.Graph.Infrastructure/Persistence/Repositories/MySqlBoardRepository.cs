using Microsoft.EntityFrameworkCore;
using ONA.Graph.Application.Abstractions;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Entities;

namespace ONA.Graph.Infrastructure.Persistence.Repositories;

internal sealed class MySqlBoardRepository(GraphDbContext dbContext) : IBoardRepository
{
    public async Task<IReadOnlyList<Board>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        await WithGraph(dbContext.Boards)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<Board?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        await WithGraph(dbContext.Boards)
            .SingleOrDefaultAsync(board => board.Id == id, cancellationToken);

    public async Task SaveAsync(
        Board board,
        CancellationToken cancellationToken = default)
    {
        var existingBoard = await WithGraph(dbContext.Boards)
            .SingleOrDefaultAsync(b => b.Id == board.Id, cancellationToken);

        if (existingBoard is null)
        {
            dbContext.Boards.Add(board);
        }
        else
        {
            dbContext.Entry(existingBoard).CurrentValues.SetValues(board);

            dbContext.RemoveRange(existingBoard.Nodes.Where(n => board.Nodes.All(x => x.Id != n.Id)));
            dbContext.RemoveRange(existingBoard.Connections.Where(c => board.Connections.All(x => x.Id != c.Id)));

            var incomingNodeIds = board.Nodes.Select(n => n.Id).ToHashSet();
            foreach (var node in existingBoard.Nodes.ToList())
            {
                if (!incomingNodeIds.Contains(node.Id))
                {
                    dbContext.Set<Node>().Remove(node);
                }
            }

            foreach (var node in board.Nodes)
            {
                var target = existingBoard.Nodes.FirstOrDefault(n => n.Id == node.Id);
                if (target is null) dbContext.Add(node);
                else dbContext.Entry(target).CurrentValues.SetValues(node);
            }

            foreach (var conn in board.Connections)
            {
                var target = existingBoard.Connections.FirstOrDefault(c => c.Id == conn.Id);
                if (target is null) dbContext.Add(conn);
                else dbContext.Entry(target).CurrentValues.SetValues(conn);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var board = await WithGraph(dbContext.Boards)
            .SingleOrDefaultAsync(board => board.Id == id, cancellationToken);

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