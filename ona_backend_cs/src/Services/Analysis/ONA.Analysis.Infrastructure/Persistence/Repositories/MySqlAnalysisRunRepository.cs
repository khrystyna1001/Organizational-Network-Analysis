using Microsoft.EntityFrameworkCore;
using ONA.Analysis.Application.Abstractions;
using ONA.Analysis.Domain.Aggregates;

namespace ONA.Analysis.Infrastructure.Persistence.Repositories;

internal sealed class MySqlAnalysisRunRepository(AnalysisDbContext dbContext) : IAnalysisRunRepository
{
    public async Task AddAsync(AnalysisRun run, CancellationToken cancellationToken = default)
    {
        dbContext.AnalysisRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<AnalysisRun?> GetByIdReadOnlyAsync(string id, CancellationToken cancellationToken = default) =>
        dbContext.AnalysisRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<AnalysisRun?> FindCompletedByGraphVersionAsync(
        string graphVersionId,
        CancellationToken cancellationToken = default) =>
        dbContext.AnalysisRuns.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.GraphVersionId == graphVersionId && r.Status == AnalysisStatus.Completed,
                cancellationToken);

    public async Task<IReadOnlyList<AnalysisRun>> GetAllReadOnlyAsync(
        string? boardId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AnalysisRuns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(boardId))
        {
            query = query.Where(r => r.BoardId == boardId);
        }

        return await query.OrderByDescending(r => r.StartedAt).ToListAsync(cancellationToken);
    }
}
