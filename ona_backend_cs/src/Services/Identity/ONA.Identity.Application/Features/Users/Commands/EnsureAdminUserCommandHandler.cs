using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class EnsureAdminUserCommandHandler(
    IUserRepository repository,
    IPasswordHasher hasher,
    TimeProvider clock) : ICommandHandler<EnsureAdminUserCommand, bool>
{
    public async Task<bool> HandleAsync(EnsureAdminUserCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);
        if (await repository.GetByEmailAsync(email, cancellationToken) is not null)
        {
            return false;
        }

        PasswordPolicy.EnsureStrong(command.Password);
        var admin = User.RegisterAdmin(
            email,
            new DisplayName(command.DisplayName ?? "Administrator"),
            hasher.Hash(command.Password!),
            clock.GetUtcNow().UtcDateTime);
        await repository.AddAsync(admin, cancellationToken);
        return true;
    }
}
