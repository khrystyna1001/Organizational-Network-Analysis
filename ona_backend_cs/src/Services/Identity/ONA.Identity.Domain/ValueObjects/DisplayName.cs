using ONA.Identity.Domain.Exceptions;

namespace ONA.Identity.Domain.ValueObjects;

/// <summary>Non-blank name, at most 100 characters; trimmed.</summary>
public sealed class DisplayName : IEquatable<DisplayName>
{
    public const int MaxLength = 100;

    public string Value { get; }

    public DisplayName(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength)
        {
            throw new InvalidDisplayNameException();
        }

        Value = trimmed;
    }

    public bool Equals(DisplayName? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as DisplayName);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
