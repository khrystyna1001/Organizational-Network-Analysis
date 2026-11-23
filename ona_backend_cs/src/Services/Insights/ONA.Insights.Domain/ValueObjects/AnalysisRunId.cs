using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Domain.ValueObjects;

/// <summary>Analysis run the recommendation came from (identifier only, owned by Analysis).</summary>
public sealed class AnalysisRunId : IEquatable<AnalysisRunId>
{
    public string Value { get; }

    public AnalysisRunId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidRecommendationIdentifierException(nameof(AnalysisRunId));
        }

        Value = value;
    }

    public bool Equals(AnalysisRunId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as AnalysisRunId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
