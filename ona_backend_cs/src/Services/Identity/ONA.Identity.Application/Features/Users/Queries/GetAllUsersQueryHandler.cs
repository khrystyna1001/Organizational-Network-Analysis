using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;

namespace ONA.Identity.Application.Features.Users.Queries;

public sealed class GetAllUsersQueryHandler(IUserRepository repository)
    : IQueryHandler<GetAllUsersQuery, IReadOnlyList<UserDto>>
{
    public async Task<IReadOnlyList<UserDto>> HandleAsync(GetAllUsersQuery query, CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(cancellationToken)).Select(UserDto.FromDomain).ToList();
}
