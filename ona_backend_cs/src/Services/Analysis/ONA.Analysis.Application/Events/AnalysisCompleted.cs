namespace ONA.Analysis.Application.Events;

/// <summary>
/// Published Language for Insights: an analysis run finished. Carries ids only (no metrics);
/// consumers read the run through the Analysis API. Topic <c>ona.analysis.completed</c>, key = BoardId.
/// </summary>
public sealed record AnalysisCompleted(
    string EventId,
    DateTime OccurredAt,
    string AnalysisRunId,
    string BoardId,
    string GraphVersionId);
