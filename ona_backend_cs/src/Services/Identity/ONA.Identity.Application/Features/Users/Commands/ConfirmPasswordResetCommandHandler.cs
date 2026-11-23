using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class ConfirmPasswordResetCommandHandler(
    IUserRepository repository,
    IPasswordHasher hasher,
    IResetTokenGenerator tokens,
    TimeProvider clock) : ICommandHandler<ConfirmPasswordResetCommand, bool>
{
    public async Task<bool> HandleAsync(ConfirmPasswordResetCommand command, CancellationToken cancellationToken = default)
    {
        PasswordPolicy.EnsureStrong(command.NewPassword);

        if (!Email.TryCreate(command.Email, out var address) || string.IsNullOrWhiteSpace(command.Token))
        {
            throw new InvalidResetTokenException();
        }

        var user = await repository.GetByEmailForUpdateAsync(address!, cancellationToken)
            ?? throw new InvalidResetTokenException();

        user.ResetPassword(hasher.Hash(command.NewPassword!), tokens.Hash(command.Token), clock.GetUtcNow().UtcDateTime);
        await repository.SaveAsync(user, cancellationToken);
        return true;
    }
}
