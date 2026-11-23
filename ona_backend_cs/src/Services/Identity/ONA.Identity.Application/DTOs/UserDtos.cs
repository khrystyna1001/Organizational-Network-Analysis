using ONA.Identity.Domain.Aggregates;

namespace ONA.Identity.Application.DTOs;

/// <summary>User data returned by the Identity API. Never contains the password hash or reset token.</summary>
public sealed record UserDto(string Id, string Email, string DisplayName, DateTime CreatedAt, string Role)
{
    public static UserDto FromDomain(User user) =>
        new(user.Id.Value, user.Email.Value, user.DisplayName.Value, user.CreatedAt, user.Role.ToString());
}

/// <summary>Result of a successful login.</summary>
public sealed record LoginResultDto(string AccessToken, string TokenType, int ExpiresIn, UserDto User);
