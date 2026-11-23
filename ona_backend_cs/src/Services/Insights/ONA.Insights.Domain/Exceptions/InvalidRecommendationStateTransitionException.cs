namespace ONA.Insights.Domain.Exceptions;

public sealed class InvalidRecommendationStateTransitionException : DomainException
{
    public InvalidRecommendationStateTransitionException(string status)
        : base("recommendation.invalid_state_transition", $"Recommendation is already {status}; it can be answered only once.")
    {
    }
}
