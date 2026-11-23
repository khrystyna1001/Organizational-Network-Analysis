namespace ONA.Identity.Domain.Exceptions;

public sealed class InvalidEmailException : DomainException
{
    public InvalidEmailException(string? value)
        : base("user.invalid_email", $"'{value}' is not a valid email address.")
    {
    }
}
