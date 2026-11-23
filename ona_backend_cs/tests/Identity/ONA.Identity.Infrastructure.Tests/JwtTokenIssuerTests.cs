using Microsoft.Extensions.Configuration;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Infrastructure.Security;

namespace ONA.Identity.Infrastructure.Tests;

public sealed class JwtTokenIssuerTests
{
    private const string Key = "unit-test-signing-key-0123456789abcdef-xyz";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserDto Ann = new("11111111-1111-1111-1111-111111111111", "ann@b.cc", "Ann", Now.UtcDateTime, "Admin");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static JwtTokenIssuer Issuer(int minutes = 60) =>
        new(new JwtSettings(Key, "ona-identity", "ona", minutes), new FixedClock(Now));

    private static TokenValidationParameters Parameters(string key = Key) => new()
    {
        ValidIssuer = "ona-identity",
        ValidAudience = "ona",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = false
    };

    [Fact]
    public async Task Token_ValidatesWithTheSameKey_AndCarriesTheUserClaims()
    {
        var issued = Issuer().Issue(Ann);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.AccessToken, Parameters());

        Assert.True(result.IsValid, result.Exception?.Message);
        var claims = result.Claims;
        Assert.Equal(Ann.Id, claims["sub"]);
        Assert.Equal("ann@b.cc", claims["email"]);
        Assert.Equal("Ann", claims["name"]);
        Assert.Equal("Admin", claims["role"]);
    }

    [Fact]
    public async Task Token_IsRejectedWithAnotherKey()
    {
        var issued = Issuer().Issue(Ann);

        var result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(issued.AccessToken, Parameters("another-signing-key-0123456789abcdef-xyz"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Token_ExpiresAfterTheConfiguredMinutes_UsingTheClock()
    {
        var issued = Issuer(minutes: 15).Issue(Ann);

        var jwt = new JsonWebToken(issued.AccessToken);

        Assert.Equal(15 * 60, issued.ExpiresInSeconds);
        Assert.Equal(Now.UtcDateTime.AddMinutes(15), jwt.ValidTo);
        Assert.Equal(Now.UtcDateTime, jwt.ValidFrom);
        Assert.Equal("ona-identity", jwt.Issuer);
        Assert.Contains("ona", jwt.Audiences);
    }

    [Fact]
    public async Task Token_IsRejectedAfterItExpired()
    {
        var issued = Issuer(minutes: 1).Issue(Ann);
        var parameters = Parameters();
        parameters.ValidateLifetime = true;
        parameters.LifetimeValidator = (_, expires, _, _) => Now.AddMinutes(5).UtcDateTime < expires;

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.AccessToken, parameters);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Settings_RequireALongKey()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "too-short" })
            .Build();

        Assert.Throws<InvalidOperationException>(() => JwtSettings.From(config));
    }
}
