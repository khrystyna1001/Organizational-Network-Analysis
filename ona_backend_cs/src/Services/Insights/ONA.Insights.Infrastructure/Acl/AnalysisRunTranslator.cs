using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;
using ONA.Insights.Infrastructure.Acl.ExternalContracts;

namespace ONA.Insights.Infrastructure.Acl;

/// <summary>Pure mapping Analysis DTO -> our <see cref="AnalysisRunResult"/> (no I/O).</summary>
internal sealed class AnalysisRunTranslator
{
    public AnalysisRunResult ToResult(ANALYSIS_RUN_DTO dto)
    {
        if (!Enum.TryParse<AnalysisRunStatus>(dto.Status, ignoreCase: true, out var status)
            || !Enum.IsDefined(status))
        {
            throw new AnalysisIntegrationException($"Analysis returned an unknown run status '{dto.Status}'.");
        }

        var flagged = new HashSet<string>(dto.Bottlenecks ?? []);
        try
        {
            var nodes = (dto.NodeMetrics ?? [])
                .Select(n => new AnalysisNodeResult(
                    new TargetNodeId(n.NodeId),
                    string.IsNullOrWhiteSpace(n.NodeName) ? n.NodeId! : n.NodeName,
                    n.InDegree,
                    n.OutDegree,
                    n.Betweenness,
                    n.IsBottleneck || flagged.Contains(n.NodeId!)))
                .ToList();

            return new AnalysisRunResult(
                new AnalysisRunId(dto.Id),
                new BoardId(dto.BoardId),
                new GraphVersionId(dto.GraphVersionId),
                status,
                nodes);
        }
        catch (InvalidRecommendationIdentifierException exception)
        {
            throw new AnalysisIntegrationException("Analysis returned a run with a missing identifier.", exception);
        }
    }
}
