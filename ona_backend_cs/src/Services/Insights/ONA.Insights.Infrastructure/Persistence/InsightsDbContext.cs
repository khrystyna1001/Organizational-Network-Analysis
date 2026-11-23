using Microsoft.EntityFrameworkCore;
using ONA.Insights.Domain.Aggregates;
using ONA.Insights.Domain.ValueObjects;

namespace ONA.Insights.Infrastructure.Persistence;

internal sealed class InsightsDbContext(DbContextOptions<InsightsDbContext> options) : DbContext(options)
{
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Recommendation>(entity =>
        {
            entity.ToTable("recommendations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64)
                .HasConversion(v => v.Value, v => new RecommendationId(v)).ValueGeneratedNever();
            entity.Property(e => e.AnalysisRunId).HasColumnName("analysis_run_id").HasMaxLength(64)
                .HasConversion(v => v.Value, v => new AnalysisRunId(v)).IsRequired();
            entity.Property(e => e.GraphVersionId).HasColumnName("graph_version_id").HasMaxLength(64)
                .HasConversion(v => v.Value, v => new GraphVersionId(v)).IsRequired();
            entity.Property(e => e.BoardId).HasColumnName("board_id").HasMaxLength(64)
                .HasConversion(v => v.Value, v => new BoardId(v)).IsRequired();
            entity.Property(e => e.TargetNodeId).HasColumnName("target_node_id").HasMaxLength(64)
                .HasConversion(v => v.Value, v => new TargetNodeId(v)).IsRequired();
            entity.Property(e => e.TargetNodeName).HasColumnName("target_node_name").HasMaxLength(256).IsRequired();
            entity.Property(e => e.Content).HasColumnName("content").HasColumnType("text").IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasMaxLength(16).HasConversion<string>().IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.DeliveredAt).HasColumnName("delivered_at");
            entity.Property(e => e.RespondedAt).HasColumnName("responded_at");
            entity.Property(e => e.UserComment).HasColumnName("user_comment").HasMaxLength(1000);
            entity.Property(e => e.RespondedByUserId).HasColumnName("responded_by_user_id").HasMaxLength(64);

            // One recommendation per bottleneck per run (makes regeneration idempotent).
            entity.HasIndex(e => new { e.AnalysisRunId, e.TargetNodeId }).IsUnique();
        });
    }
}
