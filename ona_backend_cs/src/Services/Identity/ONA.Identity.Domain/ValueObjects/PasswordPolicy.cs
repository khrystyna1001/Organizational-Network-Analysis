using ONA.Identity.Domain.Exceptions;

namespace ONA.Identity.Domain.ValueObjects;

/// <summary>Plaintext password rules: at least 8 characters, one letter and one digit.</summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static void EnsureStrong(string? password)
    {
        if (password is null
            || password.Length < MinLength
            || !password.Any(char.IsLetter)
            || !password.Any(char.IsDigit))
        {
            throw new WeakPasswordException();
        }
    }
}
