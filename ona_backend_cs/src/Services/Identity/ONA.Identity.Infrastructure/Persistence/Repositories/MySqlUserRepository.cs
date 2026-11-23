using Microsoft.EntityFrameworkCore;
using ONA.Identity.Application.Abstractions;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Infrastructure.Persistence.Repositories;

internal sealed class MySqlUserRepository(IdentityDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByIdReadOnlyAsync(UserId id, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetByEmailForUpdateAsync(Email email, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Users.AsNoTracking().OrderBy(u => u.CreatedAt).ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(User user, CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
