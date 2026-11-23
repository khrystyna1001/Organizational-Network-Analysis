using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ONA.Insights.Infrastructure.Auth;

/// <summary>Mints a short-lived token (role "Service") for calls made without a user, e.g. from the Kafka consumer.</summary>
internal sealed class ServiceTokenFactory(JwtSettings settings, TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public string Create()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(Lifetime),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "insights-service",
                ["name"] = "insights-service",
                ["role"] = "Service"
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
