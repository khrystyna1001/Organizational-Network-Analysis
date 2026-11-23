using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Application.Ports;

public enum AnalysisRunStatus
{
    Running,
    Completed,
    Failed
}

public sealed record AnalysisNodeResult(
    TargetNodeId NodeId,
    string NodeName,
    int InDegree,
    int OutDegree,
    double Betweenness,
    bool IsBottleneck);

/// <summary>Our view of an Analysis run (customer-supplier contract, only the fields Insights needs).</summary>
public sealed record AnalysisRunResult(
    AnalysisRunId Id,
    BoardId BoardId,
    GraphVersionId GraphVersionId,
    AnalysisRunStatus Status,
    IReadOnlyList<AnalysisNodeResult> Nodes);

public interface IAnalysisResultsPort
{
    /// <summary>Returns the run or <c>null</c> when Analysis does not know it. Technical failures throw <see cref="AnalysisIntegrationException"/>.</summary>
    Task<AnalysisRunResult?> GetRunAsync(AnalysisRunId analysisRunId, CancellationToken cancellationToken = default);
}

/// <summary>Technical failure while talking to the Analysis service (unreachable, bad payload).</summary>
public sealed class AnalysisIntegrationException : Exception
{
    public AnalysisIntegrationException(string message) : base(message)
    {
    }

    public AnalysisIntegrationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
