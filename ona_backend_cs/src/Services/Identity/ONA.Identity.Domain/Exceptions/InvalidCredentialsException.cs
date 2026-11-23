namespace ONA.Identity.Domain.Exceptions;

/// <summary>Same error for an unknown email and a wrong password (no user enumeration).</summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("user.invalid_credentials", "Email or password is incorrect.")
    {
    }
}
