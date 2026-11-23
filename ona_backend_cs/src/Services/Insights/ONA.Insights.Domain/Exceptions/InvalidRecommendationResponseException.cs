namespace ONA.Insights.Domain.Exceptions;

public sealed class InvalidRecommendationResponseException : DomainException
{
    public InvalidRecommendationResponseException(string message)
        : base("recommendation.invalid_response", message)
    {
    }
}
