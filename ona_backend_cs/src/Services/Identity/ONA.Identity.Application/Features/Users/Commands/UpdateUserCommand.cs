using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed record UpdateUserCommand(string UserId, string? DisplayName) : IRequest<UserDto>;
