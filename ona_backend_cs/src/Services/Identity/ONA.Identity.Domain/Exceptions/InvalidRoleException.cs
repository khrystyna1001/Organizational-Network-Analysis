namespace ONA.Identity.Domain.Exceptions;

public sealed class InvalidRoleException : DomainException
{
    public InvalidRoleException(string? role)
        : base("user.invalid_role", $"Role '{role}' is not valid. Use 'User' or 'Admin'.")
    {
    }
}
