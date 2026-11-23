using ONA.Analysis.Domain.Aggregates;

namespace ONA.Analysis.Application.DTOs;

public sealed record GraphMetricsDto(int NodeCount, int ConnectionCount, double Density, double AvgDegree);

public sealed record NodeMetricsDto(
    string NodeId,
    string NodeName,
    int InDegree,
    int OutDegree,
    double Betweenness,
    bool IsBottleneck);

public sealed record AnalysisRunDto(
    string Id,
    string BoardId,
    string GraphVersionId,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage,
    GraphMetricsDto? Metrics,
    IReadOnlyList<NodeMetricsDto> NodeMetrics,
    IReadOnlyList<string> Bottlenecks)
{
    public static AnalysisRunDto FromDomain(AnalysisRun run) =>
        new(
            run.Id,
            run.BoardId,
            run.GraphVersionId,
            run.Status.ToString(),
            run.StartedAt,
            run.CompletedAt,
            run.ErrorMessage,
            run.Metrics is null
                ? null
                : new GraphMetricsDto(
                    run.Metrics.NodeCount, run.Metrics.ConnectionCount, run.Metrics.Density, run.Metrics.AvgDegree),
            run.NodeMetrics
                .Select(n => new NodeMetricsDto(
                    n.NodeId, n.NodeName, n.InDegree, n.OutDegree, n.Betweenness, n.IsBottleneck))
                .ToArray(),
            run.Bottlenecks.ToArray());
}
