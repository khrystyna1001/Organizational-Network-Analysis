namespace ONA.Graph.Domain.Aggregates;

/// <summary>
/// Business states of the Board lifecycle (docs/architecture/stateDiagram-analysis.md).
/// UpdatePending is part of the target model but not implemented yet.
/// </summary>
public enum BoardLifecycle
{
    Draft = 0,
    Built = 1,
    Published = 2,
    Archived = 3
}
