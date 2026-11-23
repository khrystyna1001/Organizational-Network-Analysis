using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Infrastructure.Security;

/// <summary>Issues HS256 access tokens with claims sub, email, name, role.</summary>
internal sealed class JwtTokenIssuer(JwtSettings settings, TimeProvider clock) : ITokenIssuer
{
    private static readonly JsonWebTokenHandler Handler = new();

    public IssuedToken Issue(UserDto user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var lifetime = TimeSpan.FromMinutes(settings.ExpiresMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + lifetime,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = user.Id,
                ["email"] = user.Email,
                ["name"] = user.DisplayName,
                ["role"] = user.Role
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        return new IssuedToken(Handler.CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }
}
