using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EchoAPI.Graph.Application.DTOs;

namespace EchoAPI.Graph.Presentation.Contracts;

/// <summary>Payload for adding a connection to a board.</summary>
public sealed record CreateConnectionRequest
{
    [Required]
    public required string BoardId { get; init; }

    public string? SourceNodeId { get; init; }

    public string? TargetNodeId { get; init; }

    public double? Weight { get; init; } = 1.0;

    public CreateConnectionDto ToDto() =>
        new(BoardId, SourceNodeId, TargetNodeId, Weight);
}

/// <summary>Payload for updating a connection.</summary>
public sealed record UpdateConnectionRequest
{
    private double? _weight;

    public double? Weight
    {
        get => _weight;
        init
        {
            _weight = value;
            WeightSpecified = true;
        }
    }

    [JsonIgnore]
    public bool WeightSpecified { get; private set; }

    public UpdateConnectionDto ToDto() => new(Weight, WeightSpecified);
}
