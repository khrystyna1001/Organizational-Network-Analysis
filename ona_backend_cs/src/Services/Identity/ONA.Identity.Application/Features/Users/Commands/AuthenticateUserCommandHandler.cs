using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Domain.Exceptions;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed class AuthenticateUserCommandHandler(
    IUserRepository repository,
    IPasswordHasher hasher,
    ITokenIssuer tokenIssuer) : IRequestHandler<AuthenticateUserCommand, LoginResultDto>
{
    public async Task<LoginResultDto> Handle(AuthenticateUserCommand command, CancellationToken cancellationToken = default)
    {
        if (!Email.TryCreate(command.Email, out var email) || string.IsNullOrEmpty(command.Password))
        {
            throw new InvalidCredentialsException();
        }

        var user = await repository.GetByEmailAsync(email!, cancellationToken);
        if (user is null || !hasher.Verify(user.PasswordHash, command.Password))
        {
            throw new InvalidCredentialsException();
        }

        var dto = UserDto.FromDomain(user);
        var token = tokenIssuer.Issue(dto);
        return new LoginResultDto(token.AccessToken, "Bearer", token.ExpiresInSeconds, dto);
    }
}
