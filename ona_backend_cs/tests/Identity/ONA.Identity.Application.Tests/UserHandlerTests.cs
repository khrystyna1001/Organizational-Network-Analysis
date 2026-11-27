using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Features.Users.Commands;
using ONA.Identity.Application.Features.Users.Queries;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Tests;

public sealed class UserHandlerTests
{
    private readonly InMemoryUserRepository _repo = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeTokenIssuer _tokens = new();

    private RegisterUserCommandHandler Register() => new(_repo, _hasher, TimeProvider.System);

    [Fact]
    public async Task Register_StoresHashedPasswordAndReturnsDtoWithoutHash()
    {
        var dto = await Register().Handle(new RegisterUserCommand("Ann@Example.com", "Ann", "secret123"), default);

        var stored = Assert.Single(_repo.Users);
        Assert.Equal("ann@example.com", dto.Email);
        Assert.Equal("hashed:secret123", stored.PasswordHash);
        Assert.DoesNotContain("secret123", dto.ToString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        await Register().Handle(new RegisterUserCommand("a@b.cc", "A", "secret123"), default);

        var ex = await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            Register().Handle(new RegisterUserCommand("A@B.cc", "Other", "secret123"), default));

        Assert.Equal("user.duplicate_email", ex.Code);
        Assert.Single(_repo.Users);
    }

    [Fact]
    public async Task Register_WeakPassword_ThrowsAndStoresNothing()
    {
        var ex = await Assert.ThrowsAsync<WeakPasswordException>(() =>
            Register().Handle(new RegisterUserCommand("a@b.cc", "A", "short"), default));

        Assert.Equal("user.weak_password", ex.Code);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Register_InvalidEmail_Throws() =>
        await Assert.ThrowsAsync<InvalidEmailException>(() =>
            Register().Handle(new RegisterUserCommand("nope", "A", "secret123"), default));

    [Fact]
    public async Task Login_CorrectPassword_ReturnsUser()
    {
        await Register().Handle(new RegisterUserCommand("a@b.cc", "A", "secret123"), default);
        var handler = new AuthenticateUserCommandHandler(_repo, _hasher, _tokens);

        var result = await handler.Handle(new AuthenticateUserCommand("A@b.cc", "secret123"), default);

        Assert.Equal("a@b.cc", result.User.Email);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("token-for:a@b.cc:User", result.AccessToken);
        Assert.Equal(FakeTokenIssuer.ExpiresIn, result.ExpiresIn);
    }

    [Theory]
    [InlineData("a@b.cc", "wrongpass1")]
    [InlineData("unknown@b.cc", "secret123")]
    [InlineData("not-an-email", "secret123")]
    [InlineData("a@b.cc", "")]
    public async Task Login_Failure_ThrowsSameInvalidCredentials(string email, string password)
    {
        await Register().Handle(new RegisterUserCommand("a@b.cc", "A", "secret123"), default);
        var handler = new AuthenticateUserCommandHandler(_repo, _hasher, _tokens);

        var ex = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.Handle(new AuthenticateUserCommand(email, password), default));

        Assert.Equal("user.invalid_credentials", ex.Code);
    }

    [Fact]
    public async Task Update_ChangesDisplayName()
    {
        var created = await Register().Handle(new RegisterUserCommand("a@b.cc", "Ann", "secret123"), default);

        var dto = await new UpdateUserCommandHandler(_repo).Handle(new UpdateUserCommand(created.Id, "Anna"), default);

        Assert.Equal("Anna", dto.DisplayName);
        Assert.Equal("Anna", _repo.Users.Single().DisplayName.Value);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task Update_UnknownUser_Throws(string id)
    {
        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() =>
            new UpdateUserCommandHandler(_repo).Handle(new UpdateUserCommand(id, "X"), default));

        Assert.Equal("user.not_found", ex.Code);
    }

    [Fact]
    public async Task Update_BlankName_Throws()
    {
        var created = await Register().Handle(new RegisterUserCommand("a@b.cc", "Ann", "secret123"), default);

        await Assert.ThrowsAsync<InvalidDisplayNameException>(() =>
            new UpdateUserCommandHandler(_repo).Handle(new UpdateUserCommand(created.Id, " "), default));
    }

    [Fact]
    public async Task GetById_ReturnsUserOrNull()
    {
        var created = await Register().Handle(new RegisterUserCommand("a@b.cc", "Ann", "secret123"), default);
        var handler = new GetUserByIdQueryHandler(_repo);

        Assert.Equal(created.Id, (await handler.Handle(new GetUserByIdQuery(created.Id), default))!.Id);
        Assert.Null(await handler.Handle(new GetUserByIdQuery("bad"), default));
        Assert.Null(await handler.Handle(new GetUserByIdQuery(Guid.NewGuid().ToString())));
    }

    [Fact]
    public async Task GetAll_ReturnsAllUsers()
    {
        await Register().Handle(new RegisterUserCommand("a@b.cc", "A", "secret123"), default);
        await Register().Handle(new RegisterUserCommand("b@b.cc", "B", "secret123"), default);

        var all = await new GetAllUsersQueryHandler(_repo).Handle(new GetAllUsersQuery(), default);

        Assert.Equal(2, all.Count);
    }
}
