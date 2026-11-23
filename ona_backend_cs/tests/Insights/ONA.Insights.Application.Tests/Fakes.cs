using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Tests;

internal sealed class InMemoryRecommendationRepository : IRecommendationRepository
{
    public List<Recommendation> Items { get; } = [];
    public int SaveCount { get; private set; }

    public Task<Recommendation?> GetByIdAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id.Equals(id)));

    public Task<Recommendation?> GetByIdReadOnlyAsync(RecommendationId id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Recommendation>> ListAsync(
        AnalysisRunId? analysisRunId, RecommendationStatus? status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Recommendation>>(Items
            .Where(r => analysisRunId is null || r.AnalysisRunId.Equals(analysisRunId))
            .Where(r => status is null || r.Status == status)
            .ToList());

    public Task AddRangeAsync(IReadOnlyCollection<Recommendation> recommendations, CancellationToken cancellationToken = default)
    {
        Items.AddRange(recommendations);
        return Task.CompletedTask;
    }

    public Task SaveAsync(Recommendation recommendation, CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeAnalysisResultsPort(AnalysisRunResult? run) : IAnalysisResultsPort
{
    public Task<AnalysisRunResult?> GetRunAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken = default) =>
        Task.FromResult(run is not null && run.Id.Equals(analysisRunId) ? run : null);
}

internal sealed class FakeInsightGenerator : IInsightGeneratorPort
{
    public int Calls { get; private set; }

    public Task<string> GenerateAsync(InsightRequest request, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult($"Advice for {request.NodeName}");
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal static class Runs
{
    public static AnalysisRunResult Completed(string id = "run1", params string[] bottleneckIds)
    {
        var nodes = new[] { "n1", "n2", "n3" }
            .Select((nodeId, i) => new AnalysisNodeResult(
                new TargetNodeId(nodeId), $"Person {nodeId}", i, i + 1, i * 0.5, bottleneckIds.Contains(nodeId)))
            .ToList();
        return new AnalysisRunResult(
            new AnalysisRunId(id), new BoardId("b1"), new GraphVersionId("v1"), AnalysisRunStatus.Completed, nodes);
    }
}
