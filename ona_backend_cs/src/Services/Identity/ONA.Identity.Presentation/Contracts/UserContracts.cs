namespace ONA.Identity.Presentation.Contracts;

public sealed record RegisterUserRequest(string? Email, string? DisplayName, string? Password);

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UpdateUserRequest(string? DisplayName);

public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);

public sealed record ChangeRoleRequest(string? Role);

public sealed record PasswordResetRequest(string? Email);

public sealed record ConfirmPasswordResetRequest(string? Email, string? Token, string? NewPassword);
