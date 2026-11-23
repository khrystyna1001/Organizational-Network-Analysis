namespace ONA.Insights.Domain.Exceptions;

public sealed class InvalidRecommendationContentException : DomainException
{
    public InvalidRecommendationContentException(string field)
        : base("recommendation.invalid_content", $"{field} must not be empty.")
    {
    }
}
