namespace ONA.Identity.Application.Abstractions;

public interface IResetTokenGenerator
{
    /// <summary>A new random, URL-safe, single-use token (sent to the user).</summary>
    string Generate();

    /// <summary>One-way hash of a token; only the hash is stored.</summary>
    string Hash(string token);
}
