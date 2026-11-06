using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTrashRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TrashRetentionDays",
                table: "ServerSettings",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.UpdateData(
                table: "ServerSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "TrashRetentionDays",
                value: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrashRetentionDays",
                table: "ServerSettings");
        }
    }
}
