using System.Text.Json;

namespace ONA.Analysis.Infrastructure.Acl.Events;

/// <summary>Our view of the Graph event GraphVersionPublished (topic ona.graph.version-published).</summary>
internal sealed record GraphVersionPublishedMessage(string EventId, string BoardId, string GraphVersionId);

/// <summary>Pure translation of the foreign event JSON into our message type (no I/O).</summary>
internal static class GraphEventTranslator
{
    public static bool TryParseGraphVersionPublished(string? json, out GraphVersionPublishedMessage message)
    {
        message = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !TryString(root, "board_id", out var boardId) ||
                !TryString(root, "graph_version_id", out var versionId))
            {
                return false;
            }

            TryString(root, "event_id", out var eventId);
            message = new GraphVersionPublishedMessage(eventId ?? string.Empty, boardId!, versionId!);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryString(JsonElement root, string name, out string? value)
    {
        value = null;
        if (root.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(property.GetString()))
        {
            value = property.GetString();
            return true;
        }

        return false;
    }
}
