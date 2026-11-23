using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Domain.ValueObjects;

/// <summary>Immutable graph snapshot the analysis read (owned by Graph).</summary>
public sealed class GraphVersionId : IEquatable<GraphVersionId>
{
    public string Value { get; }

    public GraphVersionId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidRecommendationIdentifierException(nameof(GraphVersionId));
        }

        Value = value;
    }

    public bool Equals(GraphVersionId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as GraphVersionId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
