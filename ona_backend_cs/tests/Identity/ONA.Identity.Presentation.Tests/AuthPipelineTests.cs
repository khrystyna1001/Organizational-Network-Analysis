using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ONA.Identity.Presentation.Tests;

public sealed class AuthPipelineTests : IClassFixture<IdentityApiFactory>
{
    private const string UserPassword = "Passw0rdX";
    private const string AdminPassword = "Admin123!";
    private readonly IdentityApiFactory _factory;

    public AuthPipelineTests(IdentityApiFactory factory)
    {
        _factory = factory;
        EnsureSeeded();
    }

    private void EnsureSeeded()
    {
        lock (_factory)
        {
            if (_factory.Users.Users.Count == 0)
            {
                _factory.SeedUser("admin@ona.local", AdminPassword, admin: true);
                _factory.SeedUser("user@ona.local", UserPassword, admin: false);
            }
        }
    }

    private static readonly JsonSerializerOptions Snake = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/users/login", new { email, password }, Snake);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Json(response)).GetProperty("access_token").GetString()!;
    }

    private HttpClient Client(string? token = null)
    {
        var client = _factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    // ---- authentication / authorization ----

    [Fact]
    public async Task Health_IsAnonymous() =>
        Assert.Equal(HttpStatusCode.OK, (await Client().GetAsync("/")).StatusCode);

    [Fact]
    public async Task ListUsers_WithoutToken_Is401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/v1/users")).StatusCode);

    [Fact]
    public async Task ListUsers_AsUser_Is403()
    {
        var token = await LoginAsync(Client(), "user@ona.local", UserPassword);

        Assert.Equal(HttpStatusCode.Forbidden, (await Client(token).GetAsync("/api/v1/users")).StatusCode);
    }

    [Fact]
    public async Task ListUsers_AsAdmin_Is200AndNeverExposesHashes()
    {
        var token = await LoginAsync(Client(), "admin@ona.local", AdminPassword);

        var response = await Client(token).GetAsync("/api/v1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("admin@ona.local", body);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ReturnsBearerTokenAndUser()
    {
        var response = await Client().PostAsJsonAsync("/api/v1/users/login",
            new { email = "user@ona.local", password = UserPassword }, Snake);

        var json = await Json(response);
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
        Assert.Equal(3600, json.GetProperty("expires_in").GetInt32());
        Assert.Equal("User", json.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Is401WithCode()
    {
        var response = await Client().PostAsJsonAsync("/api/v1/users/login",
            new { email = "user@ona.local", password = "nope12345" }, Snake);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("user.invalid_credentials", (await Json(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Token_SignedWithAnotherKey_Is401()
    {
        var forged = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "ona-identity",
            Audience = "ona",
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString(), ["role"] = "Admin" },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-completely-different-key-0123456789abcdef")),
                SecurityAlgorithms.HmacSha256)
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(forged).GetAsync("/api/v1/users")).StatusCode);
    }

    // ---- registration, /me, password ----

    [Fact]
    public async Task Register_Login_Me_UpdateAndChangePassword_Flow()
    {
        var anon = Client();
        var register = await anon.PostAsJsonAsync("/api/v1/users",
            new { email = "flow@ona.local", display_name = "Flow", password = UserPassword }, Snake);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.Equal("User", (await Json(register)).GetProperty("role").GetString());

        var token = await LoginAsync(anon, "flow@ona.local", UserPassword);
        var me = Client(token);

        var profile = await Json(await me.GetAsync("/api/v1/users/me"));
        Assert.Equal("flow@ona.local", profile.GetProperty("email").GetString());

        var renamed = await me.PutAsJsonAsync("/api/v1/users/me", new { display_name = "Flowy" }, Snake);
        Assert.Equal("Flowy", (await Json(renamed)).GetProperty("display_name").GetString());

        var wrong = await me.PostAsJsonAsync("/api/v1/users/me/password",
            new { current_password = "wrong", new_password = "BrandNew99" }, Snake);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        var weak = await me.PostAsJsonAsync("/api/v1/users/me/password",
            new { current_password = UserPassword, new_password = "short" }, Snake);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);

        var changed = await me.PostAsJsonAsync("/api/v1/users/me/password",
            new { current_password = UserPassword, new_password = "BrandNew99" }, Snake);
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        await LoginAsync(anon, "flow@ona.local", "BrandNew99");
    }

    [Fact]
    public async Task Me_WithoutToken_Is401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/api/v1/users/me")).StatusCode);

    // ---- roles ----

    [Fact]
    public async Task ChangeRole_AdminPromotesUser_UserCannotChangeRoles_AdminCannotChangeSelf()
    {
        var adminToken = await LoginAsync(Client(), "admin@ona.local", AdminPassword);
        var userToken = await LoginAsync(Client(), "user@ona.local", UserPassword);
        var admin = Client(adminToken);
        var target = (await Json(await admin.GetAsync("/api/v1/users"))).EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "user@ona.local");
        var targetId = target.GetProperty("id").GetString();
        var adminId = (await Json(await admin.GetAsync("/api/v1/users/me"))).GetProperty("id").GetString();

        var forbidden = await Client(userToken).PutAsJsonAsync($"/api/v1/users/{targetId}/role", new { role = "Admin" }, Snake);
        var self = await admin.PutAsJsonAsync($"/api/v1/users/{adminId}/role", new { role = "User" }, Snake);
        var bad = await admin.PutAsJsonAsync($"/api/v1/users/{targetId}/role", new { role = "root" }, Snake);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);
        Assert.Equal("user.cannot_change_own_role", (await Json(self)).GetProperty("code").GetString());
        Assert.Equal("user.invalid_role", (await Json(bad)).GetProperty("code").GetString());

        // promote a dedicated user so the other tests keep their fixtures
        await Client().PostAsJsonAsync("/api/v1/users",
            new { email = "promo@ona.local", display_name = "Promo", password = UserPassword }, Snake);
        var promoId = (await Json(await admin.GetAsync("/api/v1/users"))).EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == "promo@ona.local").GetProperty("id").GetString();
        var ok = await admin.PutAsJsonAsync($"/api/v1/users/{promoId}/role", new { role = "Admin" }, Snake);
        Assert.Equal("Admin", (await Json(ok)).GetProperty("role").GetString());

        var promoToken = await LoginAsync(Client(), "promo@ona.local", UserPassword);
        Assert.Equal(HttpStatusCode.OK, (await Client(promoToken).GetAsync("/api/v1/users")).StatusCode);
    }

    // ---- password reset ----

    [Fact]
    public async Task PasswordReset_UnknownEmail_Is202AndSendsNothing()
    {
        var before = _factory.Emails.Sent.Count;

        var response = await Client().PostAsJsonAsync("/api/v1/users/password-reset/request",
            new { email = "ghost@ona.local" }, Snake);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(before, _factory.Emails.Sent.Count);
    }

    [Fact]
    public async Task PasswordReset_RequestThenConfirm_AllowsLoginWithNewPassword_TokenIsSingleUse()
    {
        var anon = Client();
        await anon.PostAsJsonAsync("/api/v1/users",
            new { email = "reset@ona.local", display_name = "Reset", password = UserPassword }, Snake);

        var request = await anon.PostAsJsonAsync("/api/v1/users/password-reset/request",
            new { email = "reset@ona.local" }, Snake);
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var mail = _factory.Emails.Sent.Last(m => m.To == "reset@ona.local");
        var token = mail.Body.Split('\n').Select(l => l.Trim()).First(l => l.Length == 43);

        var confirm = await anon.PostAsJsonAsync("/api/v1/users/password-reset/confirm",
            new { email = "reset@ona.local", token, new_password = "Fresh1234" }, Snake);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
        await LoginAsync(anon, "reset@ona.local", "Fresh1234");

        var reuse = await anon.PostAsJsonAsync("/api/v1/users/password-reset/confirm",
            new { email = "reset@ona.local", token, new_password = "Another123" }, Snake);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reuse.StatusCode);
        Assert.Equal("user.invalid_reset_token", (await Json(reuse)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task PasswordReset_WrongToken_Is422() =>
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await Client().PostAsJsonAsync("/api/v1/users/password-reset/confirm",
                new { email = "user@ona.local", token = "nope", new_password = "Fresh1234" }, Snake)).StatusCode);
}
