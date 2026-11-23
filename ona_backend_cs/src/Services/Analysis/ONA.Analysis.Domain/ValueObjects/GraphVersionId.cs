using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Domain.ValueObjects;

/// <summary>Identifies the immutable Graph snapshot a run reads (owned by Graph). Rejects null, empty and whitespace-only values.</summary>
public sealed class GraphVersionId : IEquatable<GraphVersionId>
{
    public string Value { get; }

    public GraphVersionId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidIdentifierException(nameof(GraphVersionId), value);
        }

        Value = value;
    }

    public bool Equals(GraphVersionId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as GraphVersionId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
