using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ONA.Analysis.Infrastructure.Acl;

/// <summary>
/// Mints a short-lived service token (role "Service") for calls made without a user request,
/// e.g. from the Kafka consumer. Uses the shared JWT settings (Jwt:Key / Issuer / Audience).
/// </summary>
internal sealed class ServiceTokenFactory(IConfiguration configuration, TimeProvider timeProvider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly JsonWebTokenHandler _handler = new();

    public string Create()
    {
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Configuration 'Jwt:Key' is not set.");
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = configuration["Jwt:Issuer"] ?? "ona-identity",
            Audience = configuration["Jwt:Audience"] ?? "ona",
            NotBefore = now,
            IssuedAt = now,
            Expires = now.Add(Lifetime),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "analysis-service",
                ["name"] = "analysis-service",
                ["role"] = "Service"
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256)
        };
        return _handler.CreateToken(descriptor);
    }
}
