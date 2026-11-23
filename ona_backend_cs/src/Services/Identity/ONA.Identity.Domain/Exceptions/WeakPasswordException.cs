namespace ONA.Identity.Domain.Exceptions;

public sealed class WeakPasswordException : DomainException
{
    public WeakPasswordException()
        : base("user.weak_password", "Password must be at least 8 characters and contain a letter and a digit.")
    {
    }
}
