namespace ONA.Analysis.Application.Notifications;

/// <summary>
/// In-process MediatR notification: a run was saved as Completed. Not a Kafka event (that is
/// <see cref="Events.AnalysisCompleted"/>); it lets the Presentation layer push the news to
/// connected browsers over SignalR without Application knowing anything about SignalR.
/// </summary>
public sealed record AnalysisRunCompletedNotification(
    string AnalysisRunId,
    string BoardId,
    string GraphVersionId,
    int BottleneckCount) : INotification;
