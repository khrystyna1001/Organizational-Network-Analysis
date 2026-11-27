namespace ONA.Insights.Application.Notifications;

/// <summary>
/// In-process MediatR notification: new recommendations were saved for a run. The Presentation layer
/// forwards it to browsers over SignalR; Application knows nothing about SignalR.
/// </summary>
public sealed record RecommendationsGeneratedNotification(
    string AnalysisRunId,
    string BoardId,
    int Count) : INotification;

/// <summary>In-process MediatR notification: a recommendation was approved or rejected.</summary>
public sealed record RecommendationRespondedNotification(
    string RecommendationId,
    string AnalysisRunId,
    string BoardId,
    string Status) : INotification;
