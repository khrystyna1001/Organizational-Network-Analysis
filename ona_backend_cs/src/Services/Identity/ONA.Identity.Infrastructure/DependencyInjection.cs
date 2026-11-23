using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Application.Features.Users.Commands;
using ONA.Identity.Application.Features.Users.Queries;
using ONA.Identity.Infrastructure.Notifications;
using ONA.Identity.Infrastructure.Persistence;
using ONA.Identity.Infrastructure.Persistence.Repositories;
using ONA.Identity.Infrastructure.Security;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ONA.Identity.Infrastructure.Tests")]

namespace ONA.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        services.AddDbContext<IdentityDbContext>((provider, options) =>
            options.UseMySQL(connectionString(provider)));
        services.AddScoped<IUserRepository, MySqlUserRepository>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IResetTokenGenerator, Sha256ResetTokenGenerator>();
        services.AddSingleton(provider => JwtSettings.From(provider.GetRequiredService<IConfiguration>()));
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IEmailSender>(provider =>
        {
            var smtp = provider.GetRequiredService<IConfiguration>().GetSection("Smtp");
            return string.IsNullOrWhiteSpace(smtp["Host"])
                ? ActivatorUtilities.CreateInstance<LoggingEmailSender>(provider)
                : new SmtpEmailSender(
                    smtp["Host"]!,
                    int.TryParse(smtp["Port"], out var port) ? port : 25,
                    smtp["From"] ?? "no-reply@ona.local");
        });

        return services.AddIdentityApplication();
    }

    /// <summary>Registers CQRS handlers explicitly (no mediator library).</summary>
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<RegisterUserCommand, UserDto>, RegisterUserCommandHandler>();
        services.AddScoped<ICommandHandler<AuthenticateUserCommand, LoginResultDto>, AuthenticateUserCommandHandler>();
        services.AddScoped<ICommandHandler<RequestPasswordResetCommand, bool>, RequestPasswordResetCommandHandler>();
        services.AddScoped<ICommandHandler<ConfirmPasswordResetCommand, bool>, ConfirmPasswordResetCommandHandler>();
        services.AddScoped<ICommandHandler<ChangePasswordCommand, bool>, ChangePasswordCommandHandler>();
        services.AddScoped<ICommandHandler<ChangeUserRoleCommand, UserDto>, ChangeUserRoleCommandHandler>();
        services.AddScoped<ICommandHandler<EnsureAdminUserCommand, bool>, EnsureAdminUserCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateUserCommand, UserDto>, UpdateUserCommandHandler>();

        services.AddScoped<IQueryHandler<GetUserByIdQuery, UserDto?>, GetUserByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetAllUsersQuery, IReadOnlyList<UserDto>>, GetAllUsersQueryHandler>();
        return services;
    }

    public static async Task InitializeIdentityDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Idempotent admin seed from Admin:Email / Admin:Password / Admin:DisplayName.
        var admin = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetSection("Admin");
        if (!string.IsNullOrWhiteSpace(admin["Email"]))
        {
            var seed = scope.ServiceProvider.GetRequiredService<ICommandHandler<EnsureAdminUserCommand, bool>>();
            var created = await seed.HandleAsync(
                new EnsureAdminUserCommand(admin["Email"], admin["Password"], admin["DisplayName"]));
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ONA.Identity.Seed")
                .LogInformation("Admin seed {Email}: {Result}", admin["Email"], created ? "created" : "already exists");
        }
    }
}
