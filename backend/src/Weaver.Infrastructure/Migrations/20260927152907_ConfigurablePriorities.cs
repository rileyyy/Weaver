using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurablePriorities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemPriorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemPriorities", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "WorkItemPriorities",
                columns: new[] { "Id", "Name", "Order" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000201"), "Low", 0 },
                    { new Guid("00000000-0000-0000-0000-000000000202"), "Medium", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000203"), "High", 2 },
                    { new Guid("00000000-0000-0000-0000-000000000204"), "Urgent", 3 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemPriorities_Order",
                table: "WorkItemPriorities",
                column: "Order",
                unique: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PriorityId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            // Hand-written: the generated migration dropped "Priority" first, which would have
            // wiped every item's priority. The old column held the enum's ordinal (Low = 0 ...
            // Urgent = 3), which is each seeded row's "Order".
            migrationBuilder.Sql(
                """
                UPDATE "WorkItems" AS w
                SET "PriorityId" = p."Id"
                FROM "WorkItemPriorities" AS p
                WHERE p."Order" = w."Priority";
                """);

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_PriorityId",
                table: "WorkItems",
                column: "PriorityId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItemPriorities_PriorityId",
                table: "WorkItems",
                column: "PriorityId",
                principalTable: "WorkItemPriorities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1 = Medium, the enum's default, matching the column AddWorkItemLayersPriorityAndAssignee created.
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "WorkItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Only the four seeded priorities have an enum value; items with none, or with a
            // priority added since, fall back to Medium.
            migrationBuilder.Sql(
                """
                UPDATE "WorkItems"
                SET "Priority" = CASE "PriorityId"
                    WHEN '00000000-0000-0000-0000-000000000201' THEN 0
                    WHEN '00000000-0000-0000-0000-000000000203' THEN 2
                    WHEN '00000000-0000-0000-0000-000000000204' THEN 3
                    ELSE 1
                END;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItemPriorities_PriorityId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_PriorityId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "PriorityId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkItemPriorities");
        }
    }
}
