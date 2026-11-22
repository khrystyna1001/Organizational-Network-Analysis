using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ONA.Graph.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lab 5: persists the Board lifecycle (State pattern) and the current GraphVersion id.
    /// Existing boards become Draft.
    /// </summary>
    public partial class AddBoardLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "lifecycle",
                table: "boards",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<string>(
                name: "current_graph_version_id",
                table: "boards",
                type: "varchar(36)",
                maxLength: 36,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_graph_version_id",
                table: "boards");

            migrationBuilder.DropColumn(
                name: "lifecycle",
                table: "boards");
        }
    }
}
