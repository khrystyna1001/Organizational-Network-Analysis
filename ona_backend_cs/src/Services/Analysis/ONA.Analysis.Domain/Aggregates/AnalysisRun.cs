using ONA.Analysis.Domain.Exceptions;
using ONA.Analysis.Domain.ValueObjects;

namespace ONA.Analysis.Domain.Aggregates;

public enum AnalysisStatus
{
    Running,
    Completed,
    Failed
}

/// <summary>
/// One execution of metrics and bottleneck detection, bound to exactly one GraphVersionId
/// so results stay reproducible. Lifecycle: Running -> Completed | Failed.
/// </summary>
public sealed class AnalysisRun
{
    private List<NodeMetrics> _nodeMetrics = [];

    public string Id { get; private set; }
    public string BoardId { get; private set; }
    public string GraphVersionId { get; private set; }
    public AnalysisStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public GraphMetrics? Metrics { get; private set; }

    public IReadOnlyCollection<NodeMetrics> NodeMetrics => _nodeMetrics;

    /// <summary>Node ids flagged as bottlenecks.</summary>
    public IReadOnlyCollection<string> Bottlenecks =>
        _nodeMetrics.Where(n => n.IsBottleneck).Select(n => n.NodeId).ToArray();

    public AnalysisRun(string? id, string? boardId, string? graphVersionId)
    {
        Id = new AnalysisRunId(id).Value;
        BoardId = new BoardId(boardId).Value;
        GraphVersionId = new GraphVersionId(graphVersionId).Value;
        Status = AnalysisStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    // Required by the persistence layer to materialize the aggregate.
    private AnalysisRun()
    {
        Id = BoardId = GraphVersionId = string.Empty;
    }

    public void Complete(NetworkAnalysisResult result)
    {
        EnsureRunning("complete");
        Metrics = result.Metrics;
        _nodeMetrics = result.NodeMetrics.ToList();
        Status = AnalysisStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        EnsureRunning("fail");
        ErrorMessage = errorMessage;
        Status = AnalysisStatus.Failed;
        CompletedAt = DateTime.UtcNow;
    }

    private void EnsureRunning(string action)
    {
        if (Status != AnalysisStatus.Running)
        {
            throw new InvalidAnalysisRunStateTransitionException(action, Status.ToString());
        }
    }
}
