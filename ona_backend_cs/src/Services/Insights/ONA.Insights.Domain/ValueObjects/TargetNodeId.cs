using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Domain.ValueObjects;

/// <summary>Node (bottleneck) the recommendation is about.</summary>
public sealed class TargetNodeId : IEquatable<TargetNodeId>
{
    public string Value { get; }

    public TargetNodeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidRecommendationIdentifierException(nameof(TargetNodeId));
        }

        Value = value;
    }

    public bool Equals(TargetNodeId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as TargetNodeId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
