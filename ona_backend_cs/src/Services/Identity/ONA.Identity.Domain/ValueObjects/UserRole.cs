namespace ONA.Identity.Domain.ValueObjects;

/// <summary>Authorization role carried in the JWT `role` claim.</summary>
public enum UserRole
{
    User = 0,
    Admin = 1
}
