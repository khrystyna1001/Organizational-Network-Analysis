namespace EchoAPI.Graph.Application.DTOs;

/// <summary>Node data returned by the Graph API.</summary>
public sealed record NodeDto(
    string Id,
    string BoardId,
    string Name,
    string Title,
    string Department);

/// <summary>Node fields supplied when adding a node to a board.</summary>
public sealed record CreateNodeDto(
    string? BoardId,
    string? Name,
    string? Title,
    string? Department);

/// <summary>Optional node fields supplied when updating a node.</summary>
public sealed record UpdateNodeDto(string? Name, string? Title, string? Department);
