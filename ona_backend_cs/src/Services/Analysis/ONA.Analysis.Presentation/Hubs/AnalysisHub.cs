using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ONA.Analysis.Presentation.Hubs;

/// <summary>
/// Server-to-client push for the analysis lifecycle, mapped at <c>/hubs/analysis</c>.
/// Browsers cannot set headers on a WebSocket, so the JWT travels in the <c>access_token</c> query
/// parameter (accepted by <c>JwtBearerEvents.OnMessageReceived</c> for <c>/hubs</c> paths only).
/// Clients only listen; the server pushes <see cref="RunCompletedMethod"/> from
/// <see cref="AnalysisRunCompletedNotificationHandler"/>.
/// </summary>
[Authorize]
public sealed class AnalysisHub : Hub
{
    public const string Path = "/hubs/analysis";
    public const string RunCompletedMethod = "analysis-run-completed";

    public static string BoardGroup(string boardId) => $"board:{boardId}";

    /// <summary>Join a board-specific group so only relevant clients get notified.</summary>
    public Task JoinBoardGroup(string boardId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, BoardGroup(boardId));

    public Task LeaveBoardGroup(string boardId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, BoardGroup(boardId));
}
