using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Loads the aggregate for modification (tracked).</summary>
    Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>Loads for display/verification only (no tracking).</summary>
    Task<User?> GetByIdReadOnlyAsync(UserId id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    /// <summary>Loads by email for modification (tracked).</summary>
    Task<User?> GetByEmailForUpdateAsync(Email email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task SaveAsync(User user, CancellationToken cancellationToken = default);
}
