using Microsoft.EntityFrameworkCore;
using ONA.Insights.Application.Abstractions;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Infrastructure.Persistence.Repositories;

internal sealed class MySqlRecommendationRepository(InsightsDbContext db) : IRecommendationRepository
{
    public Task<Recommendation?> GetByIdAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
        db.Recommendations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Recommendation?> GetByIdReadOnlyAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
        db.Recommendations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Recommendation>> ListAsync(
        AnalysisRunId? analysisRunId,
        RecommendationStatus? status,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Recommendation> query = db.Recommendations.AsNoTracking();
        if (analysisRunId is not null)
        {
            query = query.Where(r => r.AnalysisRunId == analysisRunId);
        }

        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        return await query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.TargetNodeName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IReadOnlyCollection<Recommendation> recommendations, CancellationToken cancellationToken = default)
    {
        db.Recommendations.AddRange(recommendations);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
