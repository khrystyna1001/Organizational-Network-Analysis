using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Domain.Tests;

public sealed class UserTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_SetsFieldsAndNoExternalSubject()
    {
        var user = User.Register(Email.Create("a@b.cc"), new DisplayName("Ann"), "hash", Now);

        Assert.Equal("a@b.cc", user.Email.Value);
        Assert.Equal("Ann", user.DisplayName.Value);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(Now, user.CreatedAt);
        Assert.Null(user.ExternalSubject);
        Assert.True(UserId.TryParse(user.Id.Value, out _));
    }

    [Fact]
    public void Register_GeneratesDistinctIds()
    {
        var a = User.Register(Email.Create("a@b.cc"), new DisplayName("A"), "h", Now);
        var b = User.Register(Email.Create("b@b.cc"), new DisplayName("B"), "h", Now);

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void Register_RejectsEmptyHash() =>
        Assert.Throws<ArgumentException>(() =>
            User.Register(Email.Create("a@b.cc"), new DisplayName("A"), " ", Now));

    [Fact]
    public void Rename_ChangesDisplayName()
    {
        var user = User.Register(Email.Create("a@b.cc"), new DisplayName("Ann"), "h", Now);

        user.Rename(new DisplayName("Anna"));

        Assert.Equal("Anna", user.DisplayName.Value);
    }
}

public sealed class UserRoleAndResetTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static User NewUser() => User.Register(Email.Create("a@b.cc"), new DisplayName("Ann"), "h", Now);

    [Fact]
    public void Register_CreatesUserRole_RegisterAdmin_CreatesAdmin()
    {
        Assert.Equal(UserRole.User, NewUser().Role);
        Assert.Equal(UserRole.Admin,
            User.RegisterAdmin(Email.Create("x@b.cc"), new DisplayName("X"), "h", Now).Role);
    }

    [Fact]
    public void ChangeRole_ByAnotherUser_ChangesRole()
    {
        var user = NewUser();

        user.ChangeRole(UserRole.Admin, UserId.New());

        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void ChangeRole_ByTheSameUser_ThrowsAndKeepsRole()
    {
        var user = NewUser();

        var ex = Assert.Throws<CannotChangeOwnRoleException>(() => user.ChangeRole(UserRole.Admin, user.Id));

        Assert.Equal("user.cannot_change_own_role", ex.Code);
        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public void ChangePassword_ReplacesHash()
    {
        var user = NewUser();

        user.ChangePassword("new-hash");

        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void ResetPassword_ValidToken_SetsPasswordAndClearsToken()
    {
        var user = NewUser();
        user.RequestPasswordReset("th", Now.AddMinutes(30));

        user.ResetPassword("new-hash", "th", Now.AddMinutes(1));

        Assert.Equal("new-hash", user.PasswordHash);
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Null(user.PasswordResetExpiresAt);
    }

    [Fact]
    public void ResetPassword_IsSingleUse()
    {
        var user = NewUser();
        user.RequestPasswordReset("th", Now.AddMinutes(30));
        user.ResetPassword("new-hash", "th", Now);

        var ex = Assert.Throws<InvalidResetTokenException>(() => user.ResetPassword("other", "th", Now));

        Assert.Equal("user.invalid_reset_token", ex.Code);
        Assert.Equal("new-hash", user.PasswordHash);
    }

    [Fact]
    public void ResetPassword_ExpiredToken_Throws()
    {
        var user = NewUser();
        user.RequestPasswordReset("th", Now.AddMinutes(30));

        Assert.Throws<InvalidResetTokenException>(() => user.ResetPassword("new-hash", "th", Now.AddMinutes(30)));
        Assert.Equal("h", user.PasswordHash);
    }

    [Fact]
    public void ResetPassword_WrongToken_Throws()
    {
        var user = NewUser();
        user.RequestPasswordReset("th", Now.AddMinutes(30));

        Assert.Throws<InvalidResetTokenException>(() => user.ResetPassword("new-hash", "other", Now));
    }

    [Fact]
    public void ResetPassword_WithoutRequest_Throws() =>
        Assert.Throws<InvalidResetTokenException>(() => NewUser().ResetPassword("new-hash", "th", Now));

    [Fact]
    public void RequestPasswordReset_ReplacesThePreviousToken()
    {
        var user = NewUser();
        user.RequestPasswordReset("first", Now.AddMinutes(30));
        user.RequestPasswordReset("second", Now.AddMinutes(30));

        Assert.Throws<InvalidResetTokenException>(() => user.ResetPassword("new-hash", "first", Now));
        user.ResetPassword("new-hash", "second", Now);
        Assert.Equal("new-hash", user.PasswordHash);
    }
}
