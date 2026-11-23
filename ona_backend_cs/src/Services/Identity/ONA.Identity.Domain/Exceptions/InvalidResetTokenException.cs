namespace ONA.Identity.Domain.Exceptions;

/// <summary>Unknown, wrong, expired or already used reset token – deliberately one error (no enumeration).</summary>
public sealed class InvalidResetTokenException : DomainException
{
    public InvalidResetTokenException()
        : base("user.invalid_reset_token", "The password reset token is invalid or has expired.")
    {
    }
}
