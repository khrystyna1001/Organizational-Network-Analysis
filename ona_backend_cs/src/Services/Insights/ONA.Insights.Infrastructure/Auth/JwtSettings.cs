using Microsoft.Extensions.Configuration;

namespace ONA.Insights.Infrastructure.Auth;

/// <summary>Shared JWT contract (HS256): Jwt:Key, Jwt:Issuer, Jwt:Audience.</summary>
internal sealed record JwtSettings(string Key, string Issuer, string Audience)
{
    public static JwtSettings From(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            throw new InvalidOperationException("Jwt:Key must be configured with at least 32 characters.");
        }

        return new JwtSettings(
            key,
            configuration["Jwt:Issuer"] is { Length: > 0 } issuer ? issuer : "ona-identity",
            configuration["Jwt:Audience"] is { Length: > 0 } audience ? audience : "ona");
    }
}
