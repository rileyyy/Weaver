using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerSpaceStatusesAndPriorities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemPriorities_Order",
                table: "WorkItemPriorities");

            migrationBuilder.DropIndex(
                name: "IX_Statuses_Order",
                table: "Statuses");

            // No data migration (ADR-0001: Weaver isn't deployed yet). The global statuses and
            // priorities have no Space to belong to, so they go. Work items reference them with a
            // restricting foreign key, so a database that already has work items fails here and
            // must be reset, as for AddSpaces.
            migrationBuilder.Sql("""DELETE FROM "WorkItemPriorities";""");
            migrationBuilder.Sql("""DELETE FROM "Statuses";""");

            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                table: "WorkItemPriorities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                table: "Statuses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemPriorities_SpaceId_Order",
                table: "WorkItemPriorities",
                columns: new[] { "SpaceId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Statuses_SpaceId_Order",
                table: "Statuses",
                columns: new[] { "SpaceId", "Order" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Statuses_Spaces_SpaceId",
                table: "Statuses",
                column: "SpaceId",
                principalTable: "Spaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemPriorities_Spaces_SpaceId",
                table: "WorkItemPriorities",
                column: "SpaceId",
                principalTable: "Spaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Spaces created before this migration get the defaults a new Space gets, so they
            // stay usable. The values are frozen here as SpaceDefaults had them at the time; a
            // later change to SpaceDefaults must not change what this migration did. Category
            // is stored as the StatusCategory ordinal (ToDo 0, Doing 1, Done 2).
            migrationBuilder.Sql("""
                INSERT INTO "Statuses" ("Id", "SpaceId", "Name", "Order", "Category", "Color")
                SELECT gen_random_uuid(), s."Id", d."Name", d."Order", d."Category", d."Color"
                FROM "Spaces" s
                CROSS JOIN (VALUES
                    ('To Do', 0, 0, '#1E88E5'),
                    ('Doing', 1, 1, '#FB8C00'),
                    ('Done', 2, 2, '#7B1FA2')
                ) AS d("Name", "Order", "Category", "Color");
                """);
            migrationBuilder.Sql("""
                INSERT INTO "WorkItemPriorities" ("Id", "SpaceId", "Name", "Order")
                SELECT gen_random_uuid(), s."Id", d."Name", d."Order"
                FROM "Spaces" s
                CROSS JOIN (VALUES ('Low', 0), ('Medium', 1), ('High', 2), ('Urgent', 3)) AS d("Name", "Order");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not lossless: which Space a status or priority belonged to can't be kept, so the
            // per-Space rows go and the global seeds come back. Like Up, this fails on a
            // database whose work items use them.
            migrationBuilder.Sql("""DELETE FROM "WorkItemPriorities";""");
            migrationBuilder.Sql("""DELETE FROM "Statuses";""");

            migrationBuilder.DropForeignKey(
                name: "FK_Statuses_Spaces_SpaceId",
                table: "Statuses");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemPriorities_Spaces_SpaceId",
                table: "WorkItemPriorities");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemPriorities_SpaceId_Order",
                table: "WorkItemPriorities");

            migrationBuilder.DropIndex(
                name: "IX_Statuses_SpaceId_Order",
                table: "Statuses");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "WorkItemPriorities");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "Statuses");

            migrationBuilder.InsertData(
                table: "Statuses",
                columns: new[] { "Id", "Category", "Color", "Name", "Order" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), 0, "#1E88E5", "To Do", 0 },
                    { new Guid("00000000-0000-0000-0000-000000000002"), 1, "#FB8C00", "Doing", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000003"), 2, "#7B1FA2", "Done", 2 }
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

            migrationBuilder.CreateIndex(
                name: "IX_Statuses_Order",
                table: "Statuses",
                column: "Order",
                unique: true);
        }
    }
}
