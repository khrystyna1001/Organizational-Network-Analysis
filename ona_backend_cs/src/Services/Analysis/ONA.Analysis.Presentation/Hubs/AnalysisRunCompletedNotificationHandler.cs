using Microsoft.AspNetCore.SignalR;
using ONA.Analysis.Application.Notifications;

namespace ONA.Analysis.Presentation.Hubs;

/// <summary>Payload of <see cref="AnalysisHub.RunCompletedMethod"/> (snake_case JSON, like the HTTP API).</summary>
public sealed record AnalysisRunCompletedMessage(
    string AnalysisRunId,
    string BoardId,
    string GraphVersionId,
    int BottleneckCount);

/// <summary>
/// MediatR notification handler: forwards a completed run to the clients that joined the board group.
/// Best effort: a SignalR failure is logged and never reaches the code that saved the run.
/// </summary>
public sealed class AnalysisRunCompletedNotificationHandler(
    IHubContext<AnalysisHub> hub,
    ILogger<AnalysisRunCompletedNotificationHandler> logger)
    : INotificationHandler<AnalysisRunCompletedNotification>
{
    public async Task Handle(AnalysisRunCompletedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients
                .Group(AnalysisHub.BoardGroup(notification.BoardId))
                .SendAsync(
                    AnalysisHub.RunCompletedMethod,
                    new AnalysisRunCompletedMessage(
                        notification.AnalysisRunId,
                        notification.BoardId,
                        notification.GraphVersionId,
                        notification.BottleneckCount),
                    cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Could not push analysis run {RunId} to SignalR clients.", notification.AnalysisRunId);
        }
    }
}
