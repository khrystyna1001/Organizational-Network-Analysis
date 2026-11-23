using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Tests;

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;
    public bool Verify(string hash, string password) => hash == "hashed:" + password;
}

internal sealed class FakeTokenIssuer : ITokenIssuer
{
    public const int ExpiresIn = 3600;
    public IssuedToken Issue(UserDto user) => new($"token-for:{user.Email}:{user.Role}", ExpiresIn);
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

/// <summary>Deterministic tokens: "token-1", "token-2", ... and a readable "hash(token)".</summary>
internal sealed class FakeResetTokenGenerator : IResetTokenGenerator
{
    private int _counter;
    public string Generate() => $"token-{++_counter}";
    public string Hash(string token) => $"hash({token})";
}

internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public void Advance(TimeSpan by) => _now += by;
    public override DateTimeOffset GetUtcNow() => _now;
}

internal sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id.Equals(id)));

    public Task<User?> GetByIdReadOnlyAsync(UserId id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email.Equals(email)));

    public Task<User?> GetByEmailForUpdateAsync(Email email, CancellationToken cancellationToken = default) =>
        GetByEmailAsync(email, cancellationToken);

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(Users.ToList());

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task SaveAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
