using ONA.Graph.Domain.Exceptions;

namespace ONA.Graph.Domain.ValueObjects;

/// <summary>
/// Name of a network board (docs/architecture/aggregates-and-invariants.md).
/// Rejects null, empty and whitespace-only strings; other strings are kept unchanged.
/// </summary>
public sealed class BoardName : IEquatable<BoardName>
{
    public string Value { get; }

    public BoardName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidBoardException(value);
        }

        Value = value;
    }

    public BoardName RenameTo(string? value) => new(value);

    public bool Equals(BoardName? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as BoardName);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
