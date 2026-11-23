using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Queries;

public sealed record GetAllUsersQuery : IQuery<IReadOnlyList<UserDto>>;
