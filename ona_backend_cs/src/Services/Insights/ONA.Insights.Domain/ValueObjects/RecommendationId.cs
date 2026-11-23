using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Domain.ValueObjects;

/// <summary>Identifies one Recommendation.</summary>
public sealed class RecommendationId : IEquatable<RecommendationId>
{
    public string Value { get; }

    public RecommendationId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidRecommendationIdentifierException(nameof(RecommendationId));
        }

        Value = value;
    }

    public static RecommendationId New() => new(Guid.NewGuid().ToString());

    public bool Equals(RecommendationId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as RecommendationId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
