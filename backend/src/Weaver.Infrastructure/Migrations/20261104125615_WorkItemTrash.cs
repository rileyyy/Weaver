using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WorkItemTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrashRootId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrashedAtUtc",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_TrashRootId",
                table: "WorkItems",
                column: "TrashRootId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_TrashRootId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TrashRootId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "TrashedAtUtc",
                table: "WorkItems");
        }
    }
}
