using ONA.Analysis.Domain.Exceptions;

namespace ONA.Analysis.Domain.ValueObjects;

/// <summary>Identifies the Graph board a run belongs to (owned by Graph). Rejects null, empty and whitespace-only values.</summary>
public sealed class BoardId : IEquatable<BoardId>
{
    public string Value { get; }

    public BoardId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidIdentifierException(nameof(BoardId), value);
        }

        Value = value;
    }

    public bool Equals(BoardId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as BoardId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
