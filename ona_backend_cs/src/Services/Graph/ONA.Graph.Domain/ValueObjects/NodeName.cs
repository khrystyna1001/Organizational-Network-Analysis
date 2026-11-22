using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Domain.ValueObjects;

/// <summary>
/// Name of an organization/person node (docs/architecture/aggregates-and-invariants.md).
/// Rejects null, empty and whitespace-only strings; other strings are kept unchanged.
/// </summary>
public sealed class NodeName : IEquatable<NodeName>
{
    public string Value { get; }

    public NodeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidNodeException("name", value);
        }

        Value = value;
    }

    public NodeName RenameTo(string? value) => new(value);

    public bool Equals(NodeName? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as NodeName);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
