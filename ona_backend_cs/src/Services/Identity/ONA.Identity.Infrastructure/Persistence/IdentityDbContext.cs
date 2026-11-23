using Microsoft.EntityFrameworkCore;
using ONA.Identity.Domain.Aggregates;
using ONA.Identity.Domain.ValueObjects;

namespace ONA.Identity.Infrastructure.Persistence;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Id).HasColumnName("id").HasMaxLength(36)
                .HasConversion(id => id.Value, value => UserId.From(value)).ValueGeneratedNever();
            entity.Property(u => u.Email).HasColumnName("email").HasMaxLength(Email.MaxLength).IsRequired()
                .HasConversion(email => email.Value, value => Email.Create(value));
            entity.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(DisplayName.MaxLength).IsRequired()
                .HasConversion(name => name.Value, value => new DisplayName(value));
            entity.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
            entity.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(u => u.Role).HasColumnName("role").HasMaxLength(20).IsRequired().HasConversion<string>();
            entity.Property(u => u.PasswordResetTokenHash).HasColumnName("password_reset_token_hash").HasMaxLength(64);
            entity.Property(u => u.PasswordResetExpiresAt).HasColumnName("password_reset_expires_at");
            entity.Property(u => u.ExternalSubject).HasColumnName("external_subject").HasMaxLength(256);
            entity.HasIndex(u => u.Email).IsUnique();
        });
    }
}
