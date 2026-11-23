namespace ONA.Insights.Domain.Exceptions;

public sealed class RecommendationNotFoundException : DomainException
{
    public RecommendationNotFoundException(string recommendationId)
        : base("recommendation.not_found", $"Recommendation '{recommendationId}' was not found.")
    {
    }
}
