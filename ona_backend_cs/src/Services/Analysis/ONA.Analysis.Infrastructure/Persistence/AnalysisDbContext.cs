using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ONA.Analysis.Domain.Aggregates;
using ONA.Analysis.Domain.ValueObjects;

namespace ONA.Analysis.Infrastructure.Persistence;

internal sealed class AnalysisDbContext(DbContextOptions<AnalysisDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisRun>(entity =>
        {
            entity.ToTable("analysis_runs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasMaxLength(64).ValueGeneratedNever();
            entity.Property(e => e.BoardId).HasColumnName("board_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.GraphVersionId).HasColumnName("graph_version_id").HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.StartedAt).HasColumnName("started_at")
                .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at")
                .HasConversion(
                    v => v,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(1024);

            // Results are stored as JSON text columns; they are always read and written with the run.
            entity.Property(e => e.Metrics).HasColumnName("metrics").HasColumnType("longtext")
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(ToRow(v), Json),
                    v => v == null ? null : FromRow(JsonSerializer.Deserialize<GraphMetricsRow>(v, Json)!),
                    new ValueComparer<GraphMetrics?>(
                        (a, b) => Same(a, b),
                        v => v == null ? 0 : HashCode.Combine(v.NodeCount, v.ConnectionCount, v.Density, v.AvgDegree),
                        v => v));

            entity.Property(e => e.NodeMetrics).HasColumnName("node_metrics").HasColumnType("longtext")
                .HasConversion(
                    v => JsonSerializer.Serialize(v.Select(ToRow).ToArray(), Json),
                    v => (IReadOnlyCollection<NodeMetrics>)JsonSerializer
                        .Deserialize<NodeMetricsRow[]>(v, Json)!.Select(FromRow).ToList(),
                    new ValueComparer<IReadOnlyCollection<NodeMetrics>>(
                        (a, b) => a!.SequenceEqual(b!),
                        v => v.Count,
                        v => v.ToList()));
            entity.Ignore(e => e.Bottlenecks);
            entity.HasIndex(e => e.BoardId).HasDatabaseName("ix_analysis_runs_board_id");
        });
    }

    private static bool Same(GraphMetrics? a, GraphMetrics? b) =>
        ReferenceEquals(a, b) ||
        (a is not null && b is not null && a.NodeCount == b.NodeCount && a.ConnectionCount == b.ConnectionCount
         && a.Density.Equals(b.Density) && a.AvgDegree.Equals(b.AvgDegree));

    private sealed record GraphMetricsRow(int NodeCount, int ConnectionCount, double Density, double AvgDegree);

    private sealed record NodeMetricsRow(
        string NodeId, string NodeName, int InDegree, int OutDegree, double Betweenness, bool IsBottleneck);

    private static GraphMetricsRow ToRow(GraphMetrics m) => new(m.NodeCount, m.ConnectionCount, m.Density, m.AvgDegree);

    private static GraphMetrics FromRow(GraphMetricsRow r) => new(r.NodeCount, r.ConnectionCount, r.Density, r.AvgDegree);

    private static NodeMetricsRow ToRow(NodeMetrics n) =>
        new(n.NodeId, n.NodeName, n.InDegree, n.OutDegree, n.Betweenness, n.IsBottleneck);

    private static NodeMetrics FromRow(NodeMetricsRow r) =>
        new(r.NodeId, r.NodeName, r.InDegree, r.OutDegree, r.Betweenness, r.IsBottleneck);
}
