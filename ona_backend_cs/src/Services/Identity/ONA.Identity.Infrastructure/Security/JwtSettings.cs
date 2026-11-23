using Microsoft.Extensions.Configuration;

namespace ONA.Identity.Infrastructure.Security;

/// <summary>JWT contract shared by all ONA services: HS256, issuer, audience, lifetime.</summary>
internal sealed record JwtSettings(string Key, string Issuer, string Audience, int ExpiresMinutes)
{
    public static JwtSettings From(IConfiguration configuration)
    {
        var section = configuration.GetSection("Jwt");
        var key = section["Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long.");
        }

        return new JwtSettings(
            key,
            section["Issuer"] ?? "ona-identity",
            section["Audience"] ?? "ona",
            int.TryParse(section["ExpiresMinutes"], out var minutes) && minutes > 0 ? minutes : 60);
    }
}
