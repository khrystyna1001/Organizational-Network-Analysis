using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class UpdateUserCommandHandler(IUserRepository repository)
    : ICommandHandler<UpdateUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken = default)
    {
        var displayName = new DisplayName(command.DisplayName);
        var user = UserId.TryParse(command.UserId, out var id)
            ? await repository.GetByIdAsync(id!, cancellationToken)
            : null;
        if (user is null)
        {
            throw new UserNotFoundException(command.UserId);
        }

        user.Rename(displayName);
        await repository.SaveAsync(user, cancellationToken);
        return UserDto.FromDomain(user);
    }
}
