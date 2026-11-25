using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoScriptAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceUrl",
                table: "Jobs",
                type: "TEXT",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "SourceUrl",
                table: "Jobs");
        }
    }
}
