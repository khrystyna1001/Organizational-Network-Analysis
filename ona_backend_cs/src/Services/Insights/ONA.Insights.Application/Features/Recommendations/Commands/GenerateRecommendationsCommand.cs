using ONA.Insights.Application.Abstractions;
using ONA.Insights.Application.DTOs;
using ONA.Insights.Application.Notifications;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Features.Recommendations.Commands;

/// <summary>
/// GenerateInsights + DeliverRecommendation: one recommendation per bottleneck of a Completed run.
/// Idempotent: a bottleneck that already has a recommendation for this run keeps it.
/// Publishes <see cref="RecommendationsGeneratedNotification"/> only when something new was saved.
/// </summary>
public sealed record GenerateRecommendationsCommand(string? AnalysisRunId)
    : IRequest<IReadOnlyList<RecommendationDto>>;

public sealed class GenerateRecommendationsCommandHandler(
    IRecommendationRepository repository,
    IAnalysisResultsPort analysisResults,
    IInsightGeneratorPort generator,
    TimeProvider clock,
    IPublisher notifications)
    : IRequestHandler<GenerateRecommendationsCommand, IReadOnlyList<RecommendationDto>>
{
    public async Task<IReadOnlyList<RecommendationDto>> Handle(
        GenerateRecommendationsCommand command,
        CancellationToken cancellationToken = default)
    {
        var runId = new AnalysisRunId(command.AnalysisRunId);

        var run = await analysisResults.GetRunAsync(runId, cancellationToken)
            ?? throw new AnalysisRunNotFoundException(runId.Value);

        if (run.Status != AnalysisRunStatus.Completed)
        {
            throw new AnalysisRunNotCompletedException(runId.Value, run.Status.ToString());
        }

        var bottlenecks = run.Nodes.Where(node => node.IsBottleneck).ToList();
        if (bottlenecks.Count == 0)
        {
            throw new NoBottlenecksException(runId.Value);
        }

        var existing = await repository.ListAsync(runId, null, cancellationToken);
        var known = existing.ToDictionary(r => r.TargetNodeId);

        var averageBetweenness = run.Nodes.Average(n => n.Betweenness);
        var averageDegree = run.Nodes.Average(n => n.InDegree + n.OutDegree);

        var result = new List<Recommendation>();
        var created = new List<Recommendation>();
        foreach (var node in bottlenecks)
        {
            if (known.TryGetValue(node.NodeId, out var found))
            {
                result.Add(found);
                continue;
            }

            var content = await generator.GenerateAsync(
                new InsightRequest(
                    node.NodeName,
                    node.Betweenness,
                    node.InDegree,
                    node.OutDegree,
                    averageBetweenness,
                    averageDegree,
                    run.Nodes.Count),
                cancellationToken);

            var recommendation = Recommendation.Deliver(
                RecommendationId.New(),
                run.Id,
                run.GraphVersionId,
                run.BoardId,
                node.NodeId,
                node.NodeName,
                content,
                clock.GetUtcNow().UtcDateTime);
            created.Add(recommendation);
            result.Add(recommendation);
        }

        if (created.Count > 0)
        {
            await repository.AddRangeAsync(created, cancellationToken);
            await NotifyAsync(created, cancellationToken);
        }

        return result.Select(RecommendationDto.FromDomain).ToList();
    }

    /// <summary>Best-effort UI push: the recommendations are saved, a failing handler must not undo that.</summary>
    private async Task NotifyAsync(IReadOnlyList<Recommendation> created, CancellationToken cancellationToken)
    {
        try
        {
            var first = RecommendationDto.FromDomain(created[0]);
            await notifications.Publish(
                new RecommendationsGeneratedNotification(first.AnalysisRunId, first.BoardId, created.Count),
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Live updates are optional.
        }
    }
}
