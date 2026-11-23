namespace ONA.Identity.Domain.Exceptions;

public sealed class InvalidDisplayNameException : DomainException
{
    public InvalidDisplayNameException()
        : base("user.invalid_display_name", "Display name must not be blank and at most 100 characters.")
    {
    }
}
