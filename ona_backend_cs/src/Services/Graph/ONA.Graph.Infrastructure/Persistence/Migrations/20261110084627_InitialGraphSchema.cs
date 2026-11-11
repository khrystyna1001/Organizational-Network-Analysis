using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ONA.Graph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialGraphSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "boards",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boards", x => x.id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    board_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    department = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.id);
                    table.UniqueConstraint("ak_nodes_board_id_id", x => new { x.board_id, x.id });
                    table.ForeignKey(
                        name: "fk_nodes_boards",
                        column: x => x.board_id,
                        principalTable: "boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "connections",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    board_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    source_node_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    target_node_id = table.Column<string>(type: "varchar(36)", maxLength: 36, nullable: false),
                    weight = table.Column<double>(type: "double", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connections", x => x.id);
                    table.ForeignKey(
                        name: "fk_connections_boards",
                        column: x => x.board_id,
                        principalTable: "boards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_connections_source_node",
                        columns: x => new { x.board_id, x.source_node_id },
                        principalTable: "nodes",
                        principalColumns: new[] { "board_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_connections_target_node",
                        columns: x => new { x.board_id, x.target_node_id },
                        principalTable: "nodes",
                        principalColumns: new[] { "board_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_connections_board_id_target_node_id",
                table: "connections",
                columns: new[] { "board_id", "target_node_id" });

            migrationBuilder.CreateIndex(
                name: "ux_connections_directed_pair",
                table: "connections",
                columns: new[] { "board_id", "source_node_id", "target_node_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connections");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropTable(
                name: "boards");
        }
    }
}
