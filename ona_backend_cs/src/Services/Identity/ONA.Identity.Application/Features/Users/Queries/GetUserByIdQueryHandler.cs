using ONA.Identity.Application.Abstractions;
using ONA.Identity.Application.Abstractions.Messaging;
using ONA.Identity.Application.DTOs;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Features.Users.Queries;

public sealed class GetUserByIdQueryHandler(IUserRepository repository)
    : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    public async Task<UserDto?> HandleAsync(GetUserByIdQuery query, CancellationToken cancellationToken = default)
    {
        if (!UserId.TryParse(query.UserId, out var id))
        {
            return null;
        }

        var user = await repository.GetByIdReadOnlyAsync(id!, cancellationToken);
        return user is null ? null : UserDto.FromDomain(user);
    }
}
