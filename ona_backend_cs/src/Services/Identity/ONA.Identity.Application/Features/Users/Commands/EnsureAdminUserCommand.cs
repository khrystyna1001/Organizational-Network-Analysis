using ONA.Identity.Application.Abstractions.Messaging;

namespace ONA.Identity.Application.Features.Users.Commands;

/// <summary>Startup seed: creates the administrator if the email is unused. Returns true when created.</summary>
public sealed record EnsureAdminUserCommand(string? Email, string? Password, string? DisplayName) : ICommand<bool>;
