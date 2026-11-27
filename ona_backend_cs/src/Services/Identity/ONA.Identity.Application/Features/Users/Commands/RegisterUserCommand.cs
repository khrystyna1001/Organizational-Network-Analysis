using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Commands;

/// <summary>Register User: creates a local account with a hashed password.</summary>
public sealed record RegisterUserCommand(string? Email, string? DisplayName, string? Password) : IRequest<UserDto>;
