using ONA.Identity.Application.Features.Users.Commands;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Tests;

public sealed class AuthHandlerTests
{
    private readonly InMemoryUserRepository _repo = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeResetTokenGenerator _tokens = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private async Task<string> RegisterAsync(string email = "a@b.cc", string password = "secret123")
    {
        var dto = await new RegisterUserCommandHandler(_repo, _hasher, _clock)
            .HandleAsync(new RegisterUserCommand(email, "Ann", password));
        return dto.Id;
    }

    private RequestPasswordResetCommandHandler RequestReset() => new(_repo, _tokens, _email, _clock);
    private ConfirmPasswordResetCommandHandler ConfirmReset() => new(_repo, _hasher, _tokens, _clock);

    // ---- roles ----

    [Fact]
    public async Task Register_AlwaysCreatesPlainUser()
    {
        await RegisterAsync();

        Assert.Equal(UserRole.User, _repo.Users.Single().Role);
    }

    [Fact]
    public async Task ChangeRole_ByAdmin_PromotesTargetUser()
    {
        var adminId = await SeedAdminAsync();
        var targetId = await RegisterAsync();

        var dto = await new ChangeUserRoleCommandHandler(_repo)
            .HandleAsync(new ChangeUserRoleCommand(adminId, targetId, "admin"));

        Assert.Equal("Admin", dto.Role);
        Assert.Equal(UserRole.Admin, _repo.Users.Single(u => u.Id.Value == targetId).Role);
    }

    [Fact]
    public async Task ChangeRole_OfYourself_IsDenied()
    {
        var adminId = await SeedAdminAsync();

        var ex = await Assert.ThrowsAsync<CannotChangeOwnRoleException>(() =>
            new ChangeUserRoleCommandHandler(_repo).HandleAsync(new ChangeUserRoleCommand(adminId, adminId, "User")));

        Assert.Equal("user.cannot_change_own_role", ex.Code);
        Assert.Equal(UserRole.Admin, _repo.Users.Single().Role);
    }

    [Theory]
    [InlineData("superuser")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public async Task ChangeRole_InvalidRole_Throws(string? role)
    {
        var adminId = await SeedAdminAsync();
        var targetId = await RegisterAsync();

        var ex = await Assert.ThrowsAsync<InvalidRoleException>(() =>
            new ChangeUserRoleCommandHandler(_repo).HandleAsync(new ChangeUserRoleCommand(adminId, targetId, role)));

        Assert.Equal("user.invalid_role", ex.Code);
    }

    [Fact]
    public async Task ChangeRole_UnknownTarget_Throws()
    {
        var adminId = await SeedAdminAsync();

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            new ChangeUserRoleCommandHandler(_repo)
                .HandleAsync(new ChangeUserRoleCommand(adminId, Guid.NewGuid().ToString(), "Admin")));
    }

    // ---- admin seed ----

    [Fact]
    public async Task EnsureAdmin_CreatesAdminOnce()
    {
        var handler = new EnsureAdminUserCommandHandler(_repo, _hasher, _clock);
        var command = new EnsureAdminUserCommand("admin@ona.local", "Admin123!", "Administrator");

        var first = await handler.HandleAsync(command);
        var second = await handler.HandleAsync(command);

        Assert.True(first);
        Assert.False(second);
        var admin = Assert.Single(_repo.Users);
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal("hashed:Admin123!", admin.PasswordHash);
    }

    [Fact]
    public async Task EnsureAdmin_WeakPassword_Throws() =>
        await Assert.ThrowsAsync<WeakPasswordException>(() =>
            new EnsureAdminUserCommandHandler(_repo, _hasher, _clock)
                .HandleAsync(new EnsureAdminUserCommand("admin@ona.local", "short", null)));

    private async Task<string> SeedAdminAsync()
    {
        await new EnsureAdminUserCommandHandler(_repo, _hasher, _clock)
            .HandleAsync(new EnsureAdminUserCommand("admin@ona.local", "Admin123!", "Administrator"));
        return _repo.Users.Single(u => u.Role == UserRole.Admin).Id.Value;
    }

    // ---- password reset: request ----

    [Fact]
    public async Task RequestReset_UnknownEmail_IsSilentAndSendsNothing()
    {
        await RegisterAsync();

        var sent = await RequestReset().HandleAsync(new RequestPasswordResetCommand("nobody@b.cc"));

        Assert.False(sent);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RequestReset_InvalidEmail_IsSilentAndSendsNothing()
    {
        var sent = await RequestReset().HandleAsync(new RequestPasswordResetCommand("not-an-email"));

        Assert.False(sent);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task RequestReset_KnownEmail_SendsOneEmailAndStoresOnlyTheHash()
    {
        await RegisterAsync();

        var sent = await RequestReset().HandleAsync(new RequestPasswordResetCommand("A@B.cc"));

        Assert.True(sent);
        var mail = Assert.Single(_email.Sent);
        Assert.Equal("a@b.cc", mail.To);
        Assert.Contains("token-1", mail.Body);
        var user = _repo.Users.Single();
        Assert.Equal("hash(token-1)", user.PasswordResetTokenHash);
        Assert.NotEqual("token-1", user.PasswordResetTokenHash);
        Assert.Equal(_clock.GetUtcNow().UtcDateTime.AddMinutes(30), user.PasswordResetExpiresAt);
    }

    // ---- password reset: confirm ----

    [Fact]
    public async Task ConfirmReset_ValidToken_ChangesPasswordAndConsumesToken()
    {
        await RegisterAsync();
        await RequestReset().HandleAsync(new RequestPasswordResetCommand("a@b.cc"));

        await ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("a@b.cc", "token-1", "newSecret9"));

        var user = _repo.Users.Single();
        Assert.Equal("hashed:newSecret9", user.PasswordHash);
        Assert.Null(user.PasswordResetTokenHash);
        var ex = await Assert.ThrowsAsync<InvalidResetTokenException>(() =>
            ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("a@b.cc", "token-1", "another99")));
        Assert.Equal("user.invalid_reset_token", ex.Code);
    }

    [Fact]
    public async Task ConfirmReset_WrongToken_Throws()
    {
        await RegisterAsync();
        await RequestReset().HandleAsync(new RequestPasswordResetCommand("a@b.cc"));

        await Assert.ThrowsAsync<InvalidResetTokenException>(() =>
            ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("a@b.cc", "token-999", "newSecret9")));

        Assert.Equal("hashed:secret123", _repo.Users.Single().PasswordHash);
    }

    [Fact]
    public async Task ConfirmReset_ExpiredToken_Throws()
    {
        await RegisterAsync();
        await RequestReset().HandleAsync(new RequestPasswordResetCommand("a@b.cc"));
        _clock.Advance(TimeSpan.FromMinutes(31));

        await Assert.ThrowsAsync<InvalidResetTokenException>(() =>
            ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("a@b.cc", "token-1", "newSecret9")));
    }

    [Fact]
    public async Task ConfirmReset_NewRequestReplacesOldToken()
    {
        await RegisterAsync();
        await RequestReset().HandleAsync(new RequestPasswordResetCommand("a@b.cc"));
        await RequestReset().HandleAsync(new RequestPasswordResetCommand("a@b.cc"));

        await Assert.ThrowsAsync<InvalidResetTokenException>(() =>
            ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("a@b.cc", "token-1", "newSecret9")));
        await ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("a@b.cc", "token-2", "newSecret9"));

        Assert.Equal("hashed:newSecret9", _repo.Users.Single().PasswordHash);
    }

    [Fact]
    public async Task ConfirmReset_UnknownEmail_LooksLikeAnInvalidToken() =>
        await Assert.ThrowsAsync<InvalidResetTokenException>(() =>
            ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("nobody@b.cc", "token-1", "newSecret9")));

    [Fact]
    public async Task ConfirmReset_WeakPassword_ThrowsAndKeepsToken()
    {
        await RegisterAsync();
        await RequestReset().HandleAsync(new RequestPasswordResetCommand("a@b.cc"));

        await Assert.ThrowsAsync<WeakPasswordException>(() =>
            ConfirmReset().HandleAsync(new ConfirmPasswordResetCommand("a@b.cc", "token-1", "short")));

        Assert.NotNull(_repo.Users.Single().PasswordResetTokenHash);
    }

    // ---- change password ----

    [Fact]
    public async Task ChangePassword_CorrectCurrent_UpdatesHash()
    {
        var id = await RegisterAsync();

        await new ChangePasswordCommandHandler(_repo, _hasher)
            .HandleAsync(new ChangePasswordCommand(id, "secret123", "newSecret9"));

        Assert.Equal("hashed:newSecret9", _repo.Users.Single().PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrent_ThrowsInvalidCredentials()
    {
        var id = await RegisterAsync();

        var ex = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            new ChangePasswordCommandHandler(_repo, _hasher)
                .HandleAsync(new ChangePasswordCommand(id, "wrong", "newSecret9")));

        Assert.Equal("user.invalid_credentials", ex.Code);
        Assert.Equal("hashed:secret123", _repo.Users.Single().PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_WeakNew_Throws()
    {
        var id = await RegisterAsync();

        await Assert.ThrowsAsync<WeakPasswordException>(() =>
            new ChangePasswordCommandHandler(_repo, _hasher)
                .HandleAsync(new ChangePasswordCommand(id, "secret123", "short")));

        Assert.Equal("hashed:secret123", _repo.Users.Single().PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_UnknownUser_Throws() =>
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            new ChangePasswordCommandHandler(_repo, _hasher)
                .HandleAsync(new ChangePasswordCommand(Guid.NewGuid().ToString(), "a", "newSecret9")));
}
