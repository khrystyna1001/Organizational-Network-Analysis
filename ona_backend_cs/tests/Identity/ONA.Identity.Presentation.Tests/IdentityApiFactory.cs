using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ONA.Identity.Application.Abstractions;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Presentation.Tests;

/// <summary>
/// The real HTTP pipeline (routing, JWT validation, authorization, middleware, handlers) with an
/// in-memory repository and a capturing email sender – no MySQL, no SMTP.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    public InMemoryUserRepository Users { get; } = new();
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:InitializeOnStartup", "false");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserRepository>();
            services.AddSingleton<IUserRepository>(Users);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>Creates a user straight in the repository (admins cannot be registered over HTTP).</summary>
    public void SeedUser(string email, string password, bool admin)
    {
        using var scope = Services.CreateScope();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(password);
        var address = Email.Create(email);
        var name = new DisplayName("Seeded");
        Users.Users.Add(admin
            ? User.RegisterAdmin(address, name, hash, DateTime.UtcNow)
            : User.Register(address, name, hash, DateTime.UtcNow));
    }
}

public sealed class CapturingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

public sealed class InMemoryUserRepository : IUserRepository
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
