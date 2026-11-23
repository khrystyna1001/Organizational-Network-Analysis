using ONA.Insights.Domain.Aggregates;

namespace ONA.Insights.Application.DTOs;

public sealed record RecommendationDto(
    string Id,
    string AnalysisRunId,
    string GraphVersionId,
    string BoardId,
    string TargetNodeId,
    string TargetNodeName,
    string Content,
    string Status,
    DateTime CreatedAt,
    DateTime DeliveredAt,
    DateTime? RespondedAt,
    string? UserComment,
    string? RespondedByUserId)
{
    public static RecommendationDto FromDomain(Recommendation r) =>
        new(
            r.Id.Value,
            r.AnalysisRunId.Value,
            r.GraphVersionId.Value,
            r.BoardId.Value,
            r.TargetNodeId.Value,
            r.TargetNodeName,
            r.Content,
            r.Status.ToString(),
            r.CreatedAt,
            r.DeliveredAt,
            r.RespondedAt,
            r.UserComment,
            r.RespondedByUserId);
}
