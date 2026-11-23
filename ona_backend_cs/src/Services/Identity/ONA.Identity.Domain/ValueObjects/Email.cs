using ONA.Identity.Domain.Exceptions;

namespace ONA.Identity.Domain.ValueObjects;

/// <summary>Trimmed, lower-cased email: one '@', non-empty local part, domain with a dot, max 254 chars.</summary>
public sealed class Email : IEquatable<Email>
{
    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string? value) =>
        TryCreate(value, out var email) ? email! : throw new InvalidEmailException(value);

    public static bool TryCreate(string? value, out Email? email)
    {
        email = null;
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxLength || normalized.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = normalized.IndexOf('@');
        if (at <= 0 || at != normalized.LastIndexOf('@'))
        {
            return false;
        }

        var domain = normalized[(at + 1)..];
        var dot = domain.IndexOf('.');
        if (dot <= 0 || domain.EndsWith('.'))
        {
            return false;
        }

        email = new Email(normalized);
        return true;
    }

    public bool Equals(Email? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
