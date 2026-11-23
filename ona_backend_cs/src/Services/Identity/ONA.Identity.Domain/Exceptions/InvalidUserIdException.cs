namespace ONA.Identity.Domain.Exceptions;

public sealed class InvalidUserIdException : DomainException
{
    public InvalidUserIdException(string? value)
        : base("user.invalid_id", $"'{value}' is not a valid user id.")
    {
    }
}
