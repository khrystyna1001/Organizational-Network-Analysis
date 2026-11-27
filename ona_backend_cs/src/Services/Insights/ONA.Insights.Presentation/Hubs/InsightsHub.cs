using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ONA.Insights.Presentation.Hubs;

/// <summary>
/// Server-to-client push for recommendations, mapped at <c>/hubs/insights</c>.
/// Browsers cannot set headers on a WebSocket, so the JWT travels in the <c>access_token</c> query
/// parameter (accepted by <c>JwtBearerEvents.OnMessageReceived</c> for <c>/hubs</c> paths only).
/// Clients only listen; the server pushes <see cref="RecommendationsGeneratedMethod"/> and
/// <see cref="RecommendationRespondedMethod"/> from the notification handlers in this folder.
/// </summary>
[Authorize]
public sealed class InsightsHub : Hub
{
    public const string Path = "/hubs/insights";
    public const string RecommendationsGeneratedMethod = "recommendations-generated";
    public const string RecommendationRespondedMethod = "recommendation-responded";

    public static string BoardGroup(string boardId) => $"board:{boardId}";

    public static string AnalysisRunGroup(string analysisRunId) => $"run:{analysisRunId}";

    public Task JoinBoardGroup(string boardId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(boardId));

    public Task LeaveBoardGroup(string boardId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardGroup(boardId));

    public Task JoinAnalysisRunGroup(string analysisRunId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, AnalysisRunGroup(analysisRunId));

    public Task LeaveAnalysisRunGroup(string analysisRunId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, AnalysisRunGroup(analysisRunId));
}
