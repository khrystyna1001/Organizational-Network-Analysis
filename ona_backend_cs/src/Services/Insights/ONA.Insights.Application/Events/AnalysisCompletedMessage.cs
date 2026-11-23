using System.Text.Json;

namespace ONA.Insights.Application.Events;

/// <summary>
/// Published Language from Analysis, topic <c>ona.analysis.completed</c> (key = board_id).
/// JSON snake_case: event_id, occurred_at, analysis_run_id, board_id, graph_version_id.
/// </summary>
public sealed record AnalysisCompletedMessage(
    string EventId,
    DateTimeOffset OccurredAt,
    string AnalysisRunId,
    string BoardId,
    string GraphVersionId)
{
    public const string Topic = "ona.analysis.completed";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Returns <c>null</c> for malformed JSON or a missing analysis_run_id.</summary>
    public static AnalysisCompletedMessage? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var message = JsonSerializer.Deserialize<AnalysisCompletedMessage>(json, JsonOptions);
            return message is null || string.IsNullOrWhiteSpace(message.AnalysisRunId) ? null : message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
