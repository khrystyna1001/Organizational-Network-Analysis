using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ONA.Insights.Infrastructure.Acl;
using ONA.Insights.Infrastructure.Auth;

namespace ONA.Insights.Infrastructure.Tests;

public sealed class ServiceTokenTests
{
    private const string Key = "ona-dev-signing-key-change-me-0123456789abcdef";
    private static readonly JwtSettings Settings = new(Key, "ona-identity", "ona");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static TokenValidationParameters Validation(bool lifetime = true) => new()
    {
        ValidIssuer = "ona-identity",
        ValidAudience = "ona",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ValidateLifetime = lifetime,
        RoleClaimType = "role",
        NameClaimType = "sub"
    };

    private static async Task<AuthenticationHeaderValue?> SendAsync(IHttpContextAccessor accessor, TimeProvider clock)
    {
        var inner = new RecordingHandler();
        var handler = new BearerForwardingHandler(accessor, new ServiceTokenFactory(Settings, clock)) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://analysis.test/") };
        await client.GetAsync("api/v1/analysis-runs/run1");
        return inner.Authorization;
    }

    [Fact]
    public async Task IncomingBearerHeader_IsForwardedUnchanged()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer user-token";

        var header = await SendAsync(new HttpContextAccessor { HttpContext = context }, TimeProvider.System);

        Assert.Equal("Bearer", header?.Scheme);
        Assert.Equal("user-token", header?.Parameter);
    }

    [Fact]
    public async Task NoHttpContext_UsesValidServiceToken()
    {
        var header = await SendAsync(new HttpContextAccessor(), TimeProvider.System);

        Assert.Equal("Bearer", header?.Scheme);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(header!.Parameter, Validation());
        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("Service", result.ClaimsIdentity.FindFirst("role")?.Value);
        Assert.Equal("insights-service", result.ClaimsIdentity.FindFirst("sub")?.Value);
    }

    [Fact]
    public async Task HttpContextWithoutAuthorization_UsesServiceToken()
    {
        var header = await SendAsync(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, TimeProvider.System);

        Assert.Equal("Bearer", header?.Scheme);
        Assert.False(string.IsNullOrWhiteSpace(header?.Parameter));
    }

    [Fact]
    public void ServiceToken_ExpiresAfterFiveMinutes()
    {
        var issued = new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
        var token = new JsonWebToken(new ServiceTokenFactory(Settings, new FixedClock(issued)).Create());

        Assert.Equal(issued.UtcDateTime, token.ValidFrom);
        Assert.Equal(issued.UtcDateTime.AddMinutes(5), token.ValidTo);
    }

    [Fact]
    public async Task ServiceToken_SignedWithAnotherKey_IsRejected()
    {
        var other = new JwtSettings("another-signing-key-another-signing-key-0000", "ona-identity", "ona");
        var token = new ServiceTokenFactory(other, TimeProvider.System).Create();

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, Validation());

        Assert.False(result.IsValid);
    }
}
