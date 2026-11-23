using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Domain.Tests;

public sealed class RecommendationTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Recommendation NewRecommendation() =>
        Recommendation.Deliver(
            new RecommendationId("r1"), new AnalysisRunId("run1"), new GraphVersionId("v1"),
            new BoardId("b1"), new TargetNodeId("n1"), "Alice", "Talk to Alice.", T0);

    [Fact]
    public void Deliver_StartsDeliveredWithProvenance()
    {
        var r = NewRecommendation();

        Assert.Equal(RecommendationStatus.Delivered, r.Status);
        Assert.Equal("run1", r.AnalysisRunId.Value);
        Assert.Equal("v1", r.GraphVersionId.Value);
        Assert.Equal("b1", r.BoardId.Value);
        Assert.Equal(T0, r.DeliveredAt);
        Assert.Null(r.RespondedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void Deliver_RejectsBlankContent(string? content)
    {
        var ex = Assert.Throws<InvalidRecommendationContentException>(() =>
            Recommendation.Deliver(new RecommendationId("r"), new AnalysisRunId("a"), new GraphVersionId("g"),
                new BoardId("b"), new TargetNodeId("n"), "Alice", content, T0));
        Assert.Equal("recommendation.invalid_content", ex.Code);
    }

    [Fact]
    public void Approve_RecordsDecision()
    {
        var r = NewRecommendation();

        r.Approve("ok", "user-1", T0.AddMinutes(5));

        Assert.Equal(RecommendationStatus.Approved, r.Status);
        Assert.Equal("ok", r.UserComment);
        Assert.Equal("user-1", r.RespondedByUserId);
        Assert.Equal(T0.AddMinutes(5), r.RespondedAt);
    }

    [Fact]
    public void Reject_WithoutCommentOrUser_IsAllowed()
    {
        var r = NewRecommendation();

        r.Reject(null, null, T0);

        Assert.Equal(RecommendationStatus.Rejected, r.Status);
        Assert.Null(r.UserComment);
        Assert.Null(r.RespondedByUserId);
    }

    [Fact]
    public void Respond_Twice_ThrowsInvalidStateTransitionAndKeepsFirstDecision()
    {
        var r = NewRecommendation();
        r.Approve(null, null, T0);

        var ex = Assert.Throws<InvalidRecommendationStateTransitionException>(() => r.Reject(null, null, T0));

        Assert.Equal("recommendation.invalid_state_transition", ex.Code);
        Assert.Equal(RecommendationStatus.Approved, r.Status);
    }

    [Fact]
    public void Respond_WithTooLongComment_ThrowsAndStaysDelivered()
    {
        var r = NewRecommendation();

        var ex = Assert.Throws<InvalidRecommendationResponseException>(() =>
            r.Approve(new string('x', Recommendation.MaxCommentLength + 1), null, T0));

        Assert.Equal("recommendation.invalid_response", ex.Code);
        Assert.Equal(RecommendationStatus.Delivered, r.Status);
    }

    [Fact]
    public void Respond_WithMaxLengthComment_IsAccepted()
    {
        var r = NewRecommendation();

        r.Approve(new string('x', Recommendation.MaxCommentLength), null, T0);

        Assert.Equal(RecommendationStatus.Approved, r.Status);
    }
}
