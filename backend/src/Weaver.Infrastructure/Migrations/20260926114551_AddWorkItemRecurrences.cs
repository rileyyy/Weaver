using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemRecurrences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "RecurrenceDate",
                table: "WorkItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RecurrenceSourceId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkItemRecurrences",
                columns: table => new
                {
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    GeneratedThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemRecurrences", x => x.WorkItemId);
                    table.ForeignKey(
                        name: "FK_WorkItemRecurrences_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_RecurrenceSourceId_RecurrenceDate",
                table: "WorkItems",
                columns: new[] { "RecurrenceSourceId", "RecurrenceDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItems_RecurrenceSourceId",
                table: "WorkItems",
                column: "RecurrenceSourceId",
                principalTable: "WorkItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItems_RecurrenceSourceId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkItemRecurrences");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_RecurrenceSourceId_RecurrenceDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RecurrenceDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "RecurrenceSourceId",
                table: "WorkItems");
        }
    }
}
