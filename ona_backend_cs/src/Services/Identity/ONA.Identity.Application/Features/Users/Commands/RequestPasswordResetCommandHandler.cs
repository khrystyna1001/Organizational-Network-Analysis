using ONA.Identity.Application.Abstractions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class RequestPasswordResetCommandHandler(
    IUserRepository repository,
    IResetTokenGenerator tokens,
    IEmailSender email,
    TimeProvider clock) : IRequestHandler<RequestPasswordResetCommand, bool>
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

    public async Task<bool> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken = default)
    {
        if (!Email.TryCreate(command.Email, out var address))
        {
            return false;
        }

        var user = await repository.GetByEmailForUpdateAsync(address!, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var token = tokens.Generate();
        user.RequestPasswordReset(tokens.Hash(token), clock.GetUtcNow().UtcDateTime + TokenLifetime);
        await repository.SaveAsync(user, cancellationToken);

        await email.SendAsync(
            user.Email.Value,
            "ONA password reset",
            $"Hello {user.DisplayName.Value},\n\nUse this token to reset your ONA password (valid for {TokenLifetime.TotalMinutes:0} minutes):\n\n{token}\n\nIf you did not ask for this, ignore this email.",
            cancellationToken);
        return true;
    }
}
