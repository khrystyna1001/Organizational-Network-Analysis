namespace EchoAPI.Graph.Application.DTOs;

/// <summary>Connection data returned by the Graph API.</summary>
public sealed record ConnectionDto(
    string Id,
    string BoardId,
    string SourceNodeId,
    string TargetNodeId,
    double? Weight);

/// <summary>Connection fields supplied when creating a board connection.</summary>
public sealed record CreateConnectionDto(
    string? BoardId,
    string? SourceNodeId,
    string? TargetNodeId,
    double? Weight = 1.0);

/// <summary>Optional connection fields supplied when updating a connection.</summary>
public sealed record UpdateConnectionDto(double? Weight, bool WeightSpecified);
