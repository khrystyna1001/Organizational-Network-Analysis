using Microsoft.EntityFrameworkCore;
using ONA.Graph.Domain.Aggregates;
using ONA.Graph.Domain.Entities;

namespace ONA.Graph.Infrastructure.Persistence;

internal sealed class GraphDbContext(DbContextOptions<GraphDbContext> options) : DbContext(options)
{
    public DbSet<Board> Boards => Set<Board>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Board>(entity =>
        {
            entity.ToTable("boards");
            entity.HasKey(board => board.Id);
            entity.Property(board => board.Id)
                .HasColumnName("id")
                .HasMaxLength(36)
                .ValueGeneratedNever();
            entity.Property(board => board.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(board => board.Description)
                .HasColumnName("description")
                .HasColumnType("text");
            entity.Property(board => board.Lifecycle)
                .HasColumnName("lifecycle")
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(board => board.CurrentGraphVersionId)
                .HasColumnName("current_graph_version_id")
                .HasMaxLength(36);

            entity.HasMany<Node>(nameof(Board.Nodes))
                .WithOne()
                .HasForeignKey(node => node.BoardId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_nodes_boards");
            entity.Navigation(nameof(Board.Nodes))
                .HasField("_nodes")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasMany<Connection>(nameof(Board.Connections))
                .WithOne()
                .HasForeignKey(connection => connection.BoardId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_connections_boards");
            entity.Navigation(nameof(Board.Connections))
                .HasField("_connections")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Node>(entity =>
        {
            entity.ToTable("nodes");
            entity.HasKey(node => node.Id);
            entity.Property(node => node.Id)
                .HasColumnName("id")
                .HasMaxLength(36)
                .ValueGeneratedNever();
            entity.Property(node => node.BoardId)
                .HasColumnName("board_id")
                .HasMaxLength(36)
                .IsRequired();
            entity.Property(node => node.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(node => node.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();
            entity.Property(node => node.Department)
                .HasColumnName("department")
                .HasMaxLength(255)
                .IsRequired();
            entity.HasAlternateKey(node => new { node.BoardId, node.Id })
                .HasName("ak_nodes_board_id_id");
        });

        modelBuilder.Entity<Connection>(entity =>
        {
            entity.ToTable("connections");
            entity.HasKey(connection => connection.Id);
            entity.Property(connection => connection.Id)
                .HasColumnName("id")
                .HasMaxLength(36)
                .ValueGeneratedNever();
            entity.Property(connection => connection.BoardId)
                .HasColumnName("board_id")
                .HasMaxLength(36)
                .IsRequired();
            entity.Property(connection => connection.SourceNodeId)
                .HasColumnName("source_node_id")
                .HasMaxLength(36)
                .IsRequired();
            entity.Property(connection => connection.TargetNodeId)
                .HasColumnName("target_node_id")
                .HasMaxLength(36)
                .IsRequired();
            entity.Property(connection => connection.Weight)
                .HasColumnName("weight");
            entity.HasIndex(connection => new
                {
                    connection.BoardId,
                    connection.SourceNodeId,
                    connection.TargetNodeId
                })
                .IsUnique()
                .HasDatabaseName("ux_connections_directed_pair");
            entity.HasOne<Node>()
                .WithMany()
                .HasForeignKey(connection => new { connection.BoardId, connection.SourceNodeId })
                .HasPrincipalKey(node => new { node.BoardId, node.Id })
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_connections_source_node");
            entity.HasOne<Node>()
                .WithMany()
                .HasForeignKey(connection => new { connection.BoardId, connection.TargetNodeId })
                .HasPrincipalKey(node => new { node.BoardId, node.Id })
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("fk_connections_target_node");
        });
    }
}
