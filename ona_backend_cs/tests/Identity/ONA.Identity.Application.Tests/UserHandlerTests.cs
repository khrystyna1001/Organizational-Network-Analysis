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
        var dto = await Register().HandleAsync(new RegisterUserCommand("Ann@Example.com", "Ann", "secret123"));

        var stored = Assert.Single(_repo.Users);
        Assert.Equal("ann@example.com", dto.Email);
        Assert.Equal("hashed:secret123", stored.PasswordHash);
        Assert.DoesNotContain("secret123", dto.ToString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        await Register().HandleAsync(new RegisterUserCommand("a@b.cc", "A", "secret123"));

        var ex = await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            Register().HandleAsync(new RegisterUserCommand("A@B.cc", "Other", "secret123")));

        Assert.Equal("user.duplicate_email", ex.Code);
        Assert.Single(_repo.Users);
    }

    [Fact]
    public async Task Register_WeakPassword_ThrowsAndStoresNothing()
    {
        var ex = await Assert.ThrowsAsync<WeakPasswordException>(() =>
            Register().HandleAsync(new RegisterUserCommand("a@b.cc", "A", "short")));

        Assert.Equal("user.weak_password", ex.Code);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Register_InvalidEmail_Throws() =>
        await Assert.ThrowsAsync<InvalidEmailException>(() =>
            Register().HandleAsync(new RegisterUserCommand("nope", "A", "secret123")));

    [Fact]
    public async Task Login_CorrectPassword_ReturnsUser()
    {
        await Register().HandleAsync(new RegisterUserCommand("a@b.cc", "A", "secret123"));
        var handler = new AuthenticateUserCommandHandler(_repo, _hasher, _tokens);

        var result = await handler.HandleAsync(new AuthenticateUserCommand("A@b.cc", "secret123"));

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
        await Register().HandleAsync(new RegisterUserCommand("a@b.cc", "A", "secret123"));
        var handler = new AuthenticateUserCommandHandler(_repo, _hasher, _tokens);

        var ex = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.HandleAsync(new AuthenticateUserCommand(email, password)));

        Assert.Equal("user.invalid_credentials", ex.Code);
    }

    [Fact]
    public async Task Update_ChangesDisplayName()
    {
        var created = await Register().HandleAsync(new RegisterUserCommand("a@b.cc", "Ann", "secret123"));

        var dto = await new UpdateUserCommandHandler(_repo).HandleAsync(new UpdateUserCommand(created.Id, "Anna"));

        Assert.Equal("Anna", dto.DisplayName);
        Assert.Equal("Anna", _repo.Users.Single().DisplayName.Value);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("11111111-1111-1111-1111-111111111111")]
    public async Task Update_UnknownUser_Throws(string id)
    {
        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() =>
            new UpdateUserCommandHandler(_repo).HandleAsync(new UpdateUserCommand(id, "X")));

        Assert.Equal("user.not_found", ex.Code);
    }

    [Fact]
    public async Task Update_BlankName_Throws()
    {
        var created = await Register().HandleAsync(new RegisterUserCommand("a@b.cc", "Ann", "secret123"));

        await Assert.ThrowsAsync<InvalidDisplayNameException>(() =>
            new UpdateUserCommandHandler(_repo).HandleAsync(new UpdateUserCommand(created.Id, " ")));
    }

    [Fact]
    public async Task GetById_ReturnsUserOrNull()
    {
        var created = await Register().HandleAsync(new RegisterUserCommand("a@b.cc", "Ann", "secret123"));
        var handler = new GetUserByIdQueryHandler(_repo);

        Assert.Equal(created.Id, (await handler.HandleAsync(new GetUserByIdQuery(created.Id)))!.Id);
        Assert.Null(await handler.HandleAsync(new GetUserByIdQuery("bad")));
        Assert.Null(await handler.HandleAsync(new GetUserByIdQuery(Guid.NewGuid().ToString())));
    }

    [Fact]
    public async Task GetAll_ReturnsAllUsers()
    {
        await Register().HandleAsync(new RegisterUserCommand("a@b.cc", "A", "secret123"));
        await Register().HandleAsync(new RegisterUserCommand("b@b.cc", "B", "secret123"));

        var all = await new GetAllUsersQueryHandler(_repo).HandleAsync(new GetAllUsersQuery());

        Assert.Equal(2, all.Count);
    }
}
