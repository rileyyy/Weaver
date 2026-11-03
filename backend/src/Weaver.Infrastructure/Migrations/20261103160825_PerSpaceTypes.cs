using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <summary>
    /// Types become per Space (ADR-0002). Unlike statuses and priorities, an item may have no
    /// Type, so nothing forces a reset: every Space gets a copy of each global Type and every
    /// item, and its Type history, is moved onto the copy in its own Space, matched by name
    /// (names were unique). History of deleted items keeps the old ids, which no longer resolve.
    /// </summary>
    public partial class PerSpaceTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemTypes_Order",
                table: "WorkItemTypes");

            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                table: "WorkItemTypes",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO "WorkItemTypes" ("Id", "SpaceId", "Name", "Order", "Color")
                SELECT gen_random_uuid(), s."Id", g."Name", g."Order", g."Color"
                FROM "Spaces" s
                CROSS JOIN "WorkItemTypes" g
                WHERE g."SpaceId" IS NULL;
                """);

            // History first, while the items still point at the global Types.
            foreach (var column in new[] { "OldValue", "NewValue" })
            {
                migrationBuilder.Sql($$"""
                    UPDATE "WorkItemHistory" h
                    SET "{{column}}" = to_jsonb(c."Id")
                    FROM "WorkItems" w, "WorkItemTypes" g, "WorkItemTypes" c
                    WHERE h."Field" = 'typeId'
                      AND h."WorkItemId" = w."Id"
                      AND g."SpaceId" IS NULL
                      AND h."{{column}}" #>> '{}' = g."Id"::text
                      AND c."SpaceId" = w."SpaceId"
                      AND c."Name" = g."Name";
                    """);
            }

            migrationBuilder.Sql("""
                UPDATE "WorkItems" w
                SET "TypeId" = c."Id"
                FROM "WorkItemTypes" g, "WorkItemTypes" c
                WHERE w."TypeId" = g."Id"
                  AND g."SpaceId" IS NULL
                  AND c."SpaceId" = w."SpaceId"
                  AND c."Name" = g."Name";
                """);

            migrationBuilder.Sql("""DELETE FROM "WorkItemTypes" WHERE "SpaceId" IS NULL;""");

            migrationBuilder.AlterColumn<Guid>(
                name: "SpaceId",
                table: "WorkItemTypes",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTypes_SpaceId_Order",
                table: "WorkItemTypes",
                columns: new[] { "SpaceId", "Order" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemTypes_Spaces_SpaceId",
                table: "WorkItemTypes",
                column: "SpaceId",
                principalTable: "Spaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossy where Spaces renamed or added Types: a Type is matched back onto the global
            // seed of the same name, and items whose Type has no such seed lose it.
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemTypes_Spaces_SpaceId",
                table: "WorkItemTypes");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemTypes_SpaceId_Order",
                table: "WorkItemTypes");

            migrationBuilder.AlterColumn<Guid>(
                name: "SpaceId",
                table: "WorkItemTypes",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.InsertData(
                table: "WorkItemTypes",
                columns: new[] { "Id", "Color", "Name", "Order" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), "#D81B60", "Project", 0 },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "#00897B", "Goal", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "#43A047", "Task", 2 },
                });

            foreach (var column in new[] { "OldValue", "NewValue" })
            {
                migrationBuilder.Sql($$"""
                    UPDATE "WorkItemHistory" h
                    SET "{{column}}" = to_jsonb(g."Id")
                    FROM "WorkItemTypes" t, "WorkItemTypes" g
                    WHERE h."Field" = 'typeId'
                      AND t."SpaceId" IS NOT NULL
                      AND h."{{column}}" #>> '{}' = t."Id"::text
                      AND g."SpaceId" IS NULL
                      AND g."Name" = t."Name";
                    """);
            }

            migrationBuilder.Sql("""
                UPDATE "WorkItems" w
                SET "TypeId" = g."Id"
                FROM "WorkItemTypes" t
                LEFT JOIN "WorkItemTypes" g ON g."SpaceId" IS NULL AND g."Name" = t."Name"
                WHERE w."TypeId" = t."Id" AND t."SpaceId" IS NOT NULL;
                """);

            migrationBuilder.Sql("""DELETE FROM "WorkItemTypes" WHERE "SpaceId" IS NOT NULL;""");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "WorkItemTypes");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTypes_Order",
                table: "WorkItemTypes",
                column: "Order",
                unique: true);
        }
    }
}
