namespace ONA.Insights.Domain.Exceptions;

public sealed class InvalidRecommendationIdentifierException : DomainException
{
    public InvalidRecommendationIdentifierException(string name)
        : base("recommendation.invalid_identifier", $"{name} must not be empty.")
    {
    }
}
