namespace ONA.Identity.Domain.Exceptions;

public sealed class CannotChangeOwnRoleException : DomainException
{
    public CannotChangeOwnRoleException()
        : base("user.cannot_change_own_role", "You cannot change your own role.")
    {
    }
}
