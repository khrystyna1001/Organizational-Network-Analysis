using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ONA.Graph.Infrastructure.IntegrationTests;

/// <summary>Mints JWTs that follow the shared ONA contract (HS256, iss/aud, sub/email/name/role).</summary>
internal static class TestAuth
{
    public const string Key = "integration-test-signing-key-0123456789abcdef";
    public const string Issuer = "ona-identity";
    public const string Audience = "ona";

    public static string Token(string role = "User", string subject = "user-1")
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", subject),
                new Claim("email", subject + "@example.com"),
                new Claim("name", "Test " + subject),
                new Claim("role", role)
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Graph"] = connectionString,
                    ["Jwt:Key"] = Key,
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Kafka:BootstrapServers"] = ""
                }));
        });

    public static HttpClient AuthorizedClient(this WebApplicationFactory<Program> factory, string role = "User")
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(role));
        return client;
    }
}
