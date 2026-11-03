using System.Text.Json.Serialization;
using EchoAPI.Graph.Application.DTOs;

namespace EchoAPI.Graph.Presentation.Contracts;

/// <summary>Payload for creating a graph board.</summary>
public sealed record CreateBoardRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public CreateBoardDto ToDto() => new(Name, Description);
}

/// <summary>Payload for updating a graph board.</summary>
public sealed record UpdateBoardRequest
{
    private string? _description;

    public string? Name { get; init; }

    public string? Description
    {
        get => _description;
        init
        {
            _description = value;
            DescriptionSpecified = true;
        }
    }

    [JsonIgnore]
    public bool DescriptionSpecified { get; private set; }

    public UpdateBoardDto ToDto() =>
        new(Name, Description, DescriptionSpecified);
}
