using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Abstractions;

public interface IRecommendationRepository
{
    /// <summary>Loads for modification (tracked).</summary>
    Task<Recommendation?> GetByIdAsync(RecommendationId id, CancellationToken cancellationToken = default);

    /// <summary>Loads for display only (no change tracking).</summary>
    Task<Recommendation?> GetByIdReadOnlyAsync(RecommendationId id, CancellationToken cancellationToken = default);

    /// <summary>Read-only list, newest first, optionally filtered.</summary>
    Task<IReadOnlyList<Recommendation>> ListAsync(
        AnalysisRunId? analysisRunId,
        RecommendationStatus? status,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyCollection<Recommendation> recommendations, CancellationToken cancellationToken = default);

    /// <summary>Persists changes of a loaded aggregate.</summary>
    Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default);
}
