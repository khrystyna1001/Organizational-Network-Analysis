using ONA.Identity.Application.Abstractions.Messaging;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed record ConfirmPasswordResetCommand(string? Email, string? Token, string? NewPassword) : ICommand<bool>;
