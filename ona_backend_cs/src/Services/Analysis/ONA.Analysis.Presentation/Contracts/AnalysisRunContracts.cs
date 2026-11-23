namespace ONA.Analysis.Presentation.Contracts;

/// <summary>Payload for starting an analysis run (JSON: <c>board_id</c>).</summary>
public sealed record StartAnalysisRunRequest
{
    public string? BoardId { get; init; }
}
