using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemWhen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "When",
                table: "WorkItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "When",
                table: "WorkItems");
        }
    }
}
