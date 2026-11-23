using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Domain.ValueObjects;

/// <summary>Identifies one execution of graph metrics and bottleneck detection. Rejects null, empty and whitespace-only values.</summary>
public sealed class AnalysisRunId : IEquatable<AnalysisRunId>
{
    public string Value { get; }

    public AnalysisRunId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidIdentifierException(nameof(AnalysisRunId), value);
        }

        Value = value;
    }

    public static AnalysisRunId New() => new(Guid.NewGuid().ToString());

    public bool Equals(AnalysisRunId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as AnalysisRunId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
