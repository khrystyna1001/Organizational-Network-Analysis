namespace ONA.Graph.Application.DTOs;

/// <summary>Published Language for Analysis: the immutable graph of one GraphVersion.</summary>
public sealed record GraphSnapshotDto(
    string BoardId,
    string GraphVersionId,
    string Lifecycle,
    IReadOnlyList<NodeDto> Nodes,
    IReadOnlyList<ConnectionDto> Connections);
