using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class RegisterUserCommandHandler(
    IUserRepository repository,
    IPasswordHasher hasher,
    TimeProvider clock) : ICommandHandler<RegisterUserCommand, UserDto>
{
    public async Task<UserDto> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken = default)
    {
        var email = Email.Create(command.Email);
        var displayName = new DisplayName(command.DisplayName);
        PasswordPolicy.EnsureStrong(command.Password);

        if (await repository.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw new DuplicateEmailException(email.Value);
        }

        var user = User.Register(email, displayName, hasher.Hash(command.Password!), clock.GetUtcNow().UtcDateTime);
        await repository.AddAsync(user, cancellationToken);
        return UserDto.FromDomain(user);
    }
}
