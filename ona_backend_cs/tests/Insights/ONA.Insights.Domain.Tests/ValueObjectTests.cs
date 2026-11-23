using ONA.Insights.Domain.Exceptions;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Domain.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Identifiers_RejectBlankValues(string? value)
    {
        Assert.Throws<InvalidRecommendationIdentifierException>(() => new RecommendationId(value));
        Assert.Throws<InvalidRecommendationIdentifierException>(() => new AnalysisRunId(value));
        Assert.Throws<InvalidRecommendationIdentifierException>(() => new GraphVersionId(value));
        Assert.Throws<InvalidRecommendationIdentifierException>(() => new BoardId(value));
        Assert.Throws<InvalidRecommendationIdentifierException>(() => new TargetNodeId(value));
    }

    [Fact]
    public void Identifiers_AreEqualByValue()
    {
        Assert.Equal(new AnalysisRunId("a"), new AnalysisRunId("a"));
        Assert.NotEqual(new AnalysisRunId("a"), new AnalysisRunId("b"));
        Assert.Equal(new AnalysisRunId("a").GetHashCode(), new AnalysisRunId("a").GetHashCode());
    }

    [Fact]
    public void RecommendationId_New_IsUnique()
    {
        Assert.NotEqual(RecommendationId.New(), RecommendationId.New());
    }
}
