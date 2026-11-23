using ONA.Identity.Application.Abstractions.Messaging;

namespace ONA.Identity.Application.Features.Users.Commands;

/// <summary>Always succeeds from the caller's view (no user enumeration); returns whether an email was sent.</summary>
public sealed record RequestPasswordResetCommand(string? Email) : ICommand<bool>;
