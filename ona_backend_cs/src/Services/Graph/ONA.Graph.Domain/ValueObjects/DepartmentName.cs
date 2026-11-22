using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Domain.ValueObjects;

/// <summary>
/// Department a node belongs to (docs/architecture/aggregates-and-invariants.md).
/// Rejects null, empty and whitespace-only strings; other strings are kept unchanged.
/// </summary>
public sealed class DepartmentName : IEquatable<DepartmentName>
{
    public string Value { get; }

    public DepartmentName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidNodeException("department", value);
        }

        Value = value;
    }

    public DepartmentName RenameTo(string? value) => new(value);

    public bool Equals(DepartmentName? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as DepartmentName);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
