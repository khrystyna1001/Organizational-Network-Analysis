namespace ONA.Identity.Domain.Exceptions;

public sealed class DuplicateEmailException : DomainException
{
    public DuplicateEmailException(string email)
        : base("user.duplicate_email", $"A user with email '{email}' already exists.")
    {
    }
}
