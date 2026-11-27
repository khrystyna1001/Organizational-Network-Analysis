using Microsoft.AspNetCore.SignalR;
using ONA.Insights.Application.Notifications;

namespace ONA.Insights.Presentation.Hubs;

/// <summary>Payload of <see cref="InsightsHub.RecommendationsGeneratedMethod"/> (snake_case JSON, like the HTTP API).</summary>
public sealed record RecommendationsGeneratedMessage(string AnalysisRunId, string BoardId, int Count);

/// <summary>Payload of <see cref="InsightsHub.RecommendationRespondedMethod"/>.</summary>
public sealed record RecommendationRespondedMessage(
    string RecommendationId,
    string AnalysisRunId,
    string BoardId,
    string Status);

/// <summary>
/// Forwards "recommendations generated" to the clients that joined the board or the run group.
/// Best effort: a SignalR failure is logged and never reaches the code that saved the recommendations.
/// </summary>
public sealed class RecommendationsGeneratedNotificationHandler(
    IHubContext<InsightsHub> hub,
    ILogger<RecommendationsGeneratedNotificationHandler> logger)
    : INotificationHandler<RecommendationsGeneratedNotification>
{
    public async Task Handle(RecommendationsGeneratedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients
                .Groups(new[] { InsightsHub.BoardGroup(notification.BoardId), InsightsHub.AnalysisRunGroup(notification.AnalysisRunId) })
                .SendAsync(
                    InsightsHub.RecommendationsGeneratedMethod,
                    new RecommendationsGeneratedMessage(notification.AnalysisRunId, notification.BoardId, notification.Count),
                    cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not push recommendations of run {RunId} to SignalR clients.", notification.AnalysisRunId);
        }
    }
}

/// <summary>Forwards "recommendation answered" to the clients that joined the board or the run group.</summary>
public sealed class RecommendationRespondedNotificationHandler(
    IHubContext<InsightsHub> hub,
    ILogger<RecommendationRespondedNotificationHandler> logger)
    : INotificationHandler<RecommendationRespondedNotification>
{
    public async Task Handle(RecommendationRespondedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients
                .Groups(new[] { InsightsHub.BoardGroup(notification.BoardId), InsightsHub.AnalysisRunGroup(notification.AnalysisRunId) })
                .SendAsync(
                    InsightsHub.RecommendationRespondedMethod,
                    new RecommendationRespondedMessage(
                        notification.RecommendationId,
                        notification.AnalysisRunId,
                        notification.BoardId,
                        notification.Status),
                    cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not push recommendation {RecommendationId} to SignalR clients.", notification.RecommendationId);
        }
    }
}
