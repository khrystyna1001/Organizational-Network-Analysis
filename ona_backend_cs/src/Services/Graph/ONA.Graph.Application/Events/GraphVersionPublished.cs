namespace ONA.Graph.Application.Events;

/// <summary>
/// Integration event: a board saved a new immutable GraphVersion.
/// Topic <c>ona.graph.version-published</c>, key = BoardId, JSON snake_case.
/// </summary>
public sealed record GraphVersionPublished(
    string EventId,
    DateTimeOffset OccurredAt,
    string BoardId,
    string GraphVersionId)
{
    public const string Topic = "ona.graph.version-published";
}
