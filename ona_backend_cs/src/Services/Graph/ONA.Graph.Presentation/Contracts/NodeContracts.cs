using ONA.Graph.Application.DTOs;
using System.ComponentModel.DataAnnotations;

namespace ONA.Graph.Presentation.Contracts;

/// <summary>Payload for adding a node to a board.</summary>
public sealed record CreateNodeRequest
{
    [Required]
    public required string BoardId { get; init; }

    public string? Name { get; init; }

    public string? Title { get; init; }

    public string? Department { get; init; }

    public CreateNodeDto ToDto() => new(BoardId, Name, Title, Department);
}

/// <summary>Payload for updating a node.</summary>
public sealed record UpdateNodeRequest
{
    public string? Name { get; init; }

    public string? Title { get; init; }

    public string? Department { get; init; }

    public UpdateNodeDto ToDto() =>
        new(Name, Title, Department);
}
