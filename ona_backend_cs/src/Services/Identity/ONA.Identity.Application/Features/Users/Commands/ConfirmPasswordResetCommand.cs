
namespace ONA.Identity.Application.Features.Users.Commands;

public sealed record ConfirmPasswordResetCommand(string? Email, string? Token, string? NewPassword) : IRequest<bool>;
