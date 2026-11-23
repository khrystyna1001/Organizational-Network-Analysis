namespace ONA.Insights.Domain.Aggregates;

/// <summary>Delivered -> Approved | Rejected (answered once).</summary>
public enum RecommendationStatus
{
    Delivered,
    Approved,
    Rejected
}
