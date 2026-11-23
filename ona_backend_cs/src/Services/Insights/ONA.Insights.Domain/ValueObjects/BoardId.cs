using ONA.Insights.Domain.Exceptions;

namespace ONA.Insights.Domain.ValueObjects;

/// <summary>Board of the analysed graph (owned by Graph).</summary>
public sealed class BoardId : IEquatable<BoardId>
{
    public string Value { get; }

    public BoardId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidRecommendationIdentifierException(nameof(BoardId));
        }

        Value = value;
    }

    public bool Equals(BoardId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as BoardId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
