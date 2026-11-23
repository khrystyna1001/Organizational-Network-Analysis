using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed record UpdateUserCommand(string UserId, string? DisplayName) : ICommand<UserDto>;
