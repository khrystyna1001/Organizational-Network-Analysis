using System.Security.Cryptography;
using System.Text;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Domain.Aggregates;

/// <summary>Local account. Only the password hash is stored, never the password or the reset token.</summary>
public sealed class User
{
    public UserId Id { get; }
    public Email Email { get; }
    public DisplayName DisplayName { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTime CreatedAt { get; }
    public UserRole Role { get; private set; }

    /// <summary>SHA-256 hash of the outstanding reset token (null when none).</summary>
    public string? PasswordResetTokenHash { get; private set; }

    public DateTime? PasswordResetExpiresAt { get; private set; }

    /// <summary>Subject at an external OIDC provider; not used until a provider is selected.</summary>
    public string? ExternalSubject { get; }

    private User(UserId id, Email email, DisplayName displayName, string passwordHash, DateTime createdAt, UserRole role)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
        Role = role;
    }

    /// <summary>Public registration always creates a plain <see cref="UserRole.User"/>.</summary>
    public static User Register(Email email, DisplayName displayName, string passwordHash, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return new User(UserId.New(), email, displayName, passwordHash, now, UserRole.User);
    }

    /// <summary>Used only by the startup admin seed.</summary>
    public static User RegisterAdmin(Email email, DisplayName displayName, string passwordHash, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return new User(UserId.New(), email, displayName, passwordHash, now, UserRole.Admin);
    }

    public void Rename(DisplayName displayName) => DisplayName = displayName;

    public void ChangePassword(string newPasswordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);
        PasswordHash = newPasswordHash;
    }

    /// <summary>An administrator cannot change their own role (prevents locking everyone out).</summary>
    public void ChangeRole(UserRole role, UserId actor)
    {
        if (actor.Equals(Id))
        {
            throw new CannotChangeOwnRoleException();
        }

        Role = role;
    }

    /// <summary>Stores the hash of a new reset token; a new request replaces the previous one.</summary>
    public void RequestPasswordReset(string tokenHash, DateTime expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        PasswordResetTokenHash = tokenHash;
        PasswordResetExpiresAt = expiresAt;
    }

    /// <summary>Single use: a matching, unexpired token sets the new password and is consumed.</summary>
    public void ResetPassword(string newPasswordHash, string tokenHash, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);

        if (PasswordResetTokenHash is null
            || PasswordResetExpiresAt is not { } expiresAt
            || now >= expiresAt
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(PasswordResetTokenHash),
                Encoding.UTF8.GetBytes(tokenHash ?? string.Empty)))
        {
            throw new InvalidResetTokenException();
        }

        PasswordHash = newPasswordHash;
        PasswordResetTokenHash = null;
        PasswordResetExpiresAt = null;
    }
}
