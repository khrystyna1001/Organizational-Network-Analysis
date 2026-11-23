namespace ONA.Identity.Domain.Exceptions;

public sealed class UserNotFoundException : DomainException
{
    public UserNotFoundException(string userId)
        : base("user.not_found", $"User '{userId}' was not found.")
    {
    }
}
