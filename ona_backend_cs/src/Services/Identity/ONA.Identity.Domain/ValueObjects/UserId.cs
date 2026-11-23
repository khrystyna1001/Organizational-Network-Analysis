using ONA.Identity.Domain.Exceptions;

namespace ONA.Identity.Domain.ValueObjects;

/// <summary>Our own user identifier (GUID string). Distinct from any external provider subject.</summary>
public sealed class UserId : IEquatable<UserId>
{
    public string Value { get; }

    private UserId(string value) => Value = value;

    public static UserId New() => new(Guid.NewGuid().ToString());

    public static UserId From(string? value) =>
        TryParse(value, out var id) ? id! : throw new InvalidUserIdException(value);

    public static bool TryParse(string? value, out UserId? id)
    {
        if (Guid.TryParse(value, out var guid))
        {
            id = new UserId(guid.ToString());
            return true;
        }

        id = null;
        return false;
    }

    public bool Equals(UserId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as UserId);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;
}
