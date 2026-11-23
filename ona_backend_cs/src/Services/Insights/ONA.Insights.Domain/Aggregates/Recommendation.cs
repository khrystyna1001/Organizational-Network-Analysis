using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Domain.Aggregates;

/// <summary>
/// Advice generated from one bottleneck of an analysis run. Carries provenance (run, graph version,
/// board). Approval only records the decision; it never changes a graph by itself.
/// </summary>
public sealed class Recommendation
{
    public const int MaxCommentLength = 1000;

    public RecommendationId Id { get; private set; } = null!;
    public AnalysisRunId AnalysisRunId { get; private set; } = null!;
    public GraphVersionId GraphVersionId { get; private set; } = null!;
    public BoardId BoardId { get; private set; } = null!;
    public TargetNodeId TargetNodeId { get; private set; } = null!;
    public string TargetNodeName { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public RecommendationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime DeliveredAt { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public string? UserComment { get; private set; }
    public string? RespondedByUserId { get; private set; }

    // Used by the persistence layer only.
    private Recommendation()
    {
    }

    /// <summary>Creates a recommendation that is delivered immediately.</summary>
    public static Recommendation Deliver(
        RecommendationId id,
        AnalysisRunId analysisRunId,
        GraphVersionId graphVersionId,
        BoardId boardId,
        TargetNodeId targetNodeId,
        string? targetNodeName,
        string? content,
        DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(targetNodeName))
        {
            throw new InvalidRecommendationContentException(nameof(targetNodeName));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidRecommendationContentException(nameof(content));
        }

        return new Recommendation
        {
            Id = id,
            AnalysisRunId = analysisRunId,
            GraphVersionId = graphVersionId,
            BoardId = boardId,
            TargetNodeId = targetNodeId,
            TargetNodeName = targetNodeName,
            Content = content,
            Status = RecommendationStatus.Delivered,
            CreatedAt = nowUtc,
            DeliveredAt = nowUtc
        };
    }

    public void Approve(string? comment, string? userId, DateTime nowUtc) =>
        Respond(RecommendationStatus.Approved, comment, userId, nowUtc);

    public void Reject(string? comment, string? userId, DateTime nowUtc) =>
        Respond(RecommendationStatus.Rejected, comment, userId, nowUtc);

    private void Respond(RecommendationStatus decision, string? comment, string? userId, DateTime nowUtc)
    {
        if (Status != RecommendationStatus.Delivered)
        {
            throw new InvalidRecommendationStateTransitionException(Status.ToString());
        }

        if (comment is { Length: > MaxCommentLength })
        {
            throw new InvalidRecommendationResponseException(
                $"Comment must be at most {MaxCommentLength} characters.");
        }

        Status = decision;
        RespondedAt = nowUtc;
        UserComment = string.IsNullOrWhiteSpace(comment) ? null : comment;
        RespondedByUserId = string.IsNullOrWhiteSpace(userId) ? null : userId;
    }
}
