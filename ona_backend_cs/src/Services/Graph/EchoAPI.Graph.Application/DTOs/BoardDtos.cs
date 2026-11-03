namespace EchoAPI.Graph.Application.DTOs;

/// <summary>Board data returned by the Graph API.</summary>
public sealed record BoardDto(string Id, string Name, string? Description);

/// <summary>Board fields supplied when creating a board.</summary>
public sealed record CreateBoardDto(string? Name, string? Description);

/// <summary>Optional board fields supplied when updating a board.</summary>
public sealed record UpdateBoardDto(
    string? Name,
    string? Description,
    bool DescriptionSpecified);
