using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Commands;

/// <summary>Verifies credentials and returns a signed access token plus the user.</summary>
public sealed record AuthenticateUserCommand(string? Email, string? Password) : ICommand<LoginResultDto>;
