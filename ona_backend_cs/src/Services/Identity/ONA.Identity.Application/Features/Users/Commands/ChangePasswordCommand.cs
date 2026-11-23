using ONA.Identity.Application.Abstractions.Messaging;

namespace ONA.Identity.Application.Features.Users.Commands;

public sealed record ChangePasswordCommand(string UserId, string? CurrentPassword, string? NewPassword) : ICommand<bool>;
