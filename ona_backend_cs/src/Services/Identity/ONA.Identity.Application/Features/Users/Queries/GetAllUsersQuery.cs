using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Queries;

public sealed record GetAllUsersQuery : IRequest<IReadOnlyList<UserDto>>;
