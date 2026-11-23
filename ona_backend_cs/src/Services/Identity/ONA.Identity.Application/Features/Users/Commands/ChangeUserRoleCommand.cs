using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Commands;

/// <summary>Admin changes another user's role. <paramref name="ActorUserId"/> is the caller (from the token).</summary>
public sealed record ChangeUserRoleCommand(string ActorUserId, string TargetUserId, string? Role) : ICommand<UserDto>;
