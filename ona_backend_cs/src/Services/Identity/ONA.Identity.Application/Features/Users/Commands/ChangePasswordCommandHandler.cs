using ONA.Identity.Application.Abstractions;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class ChangePasswordCommandHandler(IUserRepository repository, IPasswordHasher hasher)
    : IRequestHandler<ChangePasswordCommand, bool>
{
    public async Task<bool> Handle(ChangePasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = UserId.TryParse(command.UserId, out var id)
            ? await repository.GetByIdAsync(id!, cancellationToken)
            : null;
        if (user is null)
        {
            throw new UserNotFoundException(command.UserId);
        }

        if (string.IsNullOrEmpty(command.CurrentPassword) || !hasher.Verify(user.PasswordHash, command.CurrentPassword))
        {
            throw new InvalidCredentialsException();
        }

        PasswordPolicy.EnsureStrong(command.NewPassword);
        user.ChangePassword(hasher.Hash(command.NewPassword!));
        await repository.SaveAsync(user, cancellationToken);
        return true;
    }
}
