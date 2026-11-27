using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class ChangeUserRoleCommandHandler(IUserRepository repository)
    : IRequestHandler<ChangeUserRoleCommand, UserDto>
{
    public async Task<UserDto> Handle(ChangeUserRoleCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<UserRole>(command.Role, ignoreCase: true, out var role)
            || !Enum.IsDefined(role)
            || int.TryParse(command.Role, out _))
        {
            throw new InvalidRoleException(command.Role);
        }

        var actor = UserId.From(command.ActorUserId);
        var user = UserId.TryParse(command.TargetUserId, out var id)
            ? await repository.GetByIdAsync(id!, cancellationToken)
            : null;
        if (user is null)
        {
            throw new UserNotFoundException(command.TargetUserId);
        }

        user.ChangeRole(role, actor);
        await repository.SaveAsync(user, cancellationToken);
        return UserDto.FromDomain(user);
    }
}
