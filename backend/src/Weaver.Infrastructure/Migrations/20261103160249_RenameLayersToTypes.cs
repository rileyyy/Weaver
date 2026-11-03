using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <summary>
    /// Layer is renamed Type (ADR-0002). Written by hand as renames: the generator drops and
    /// recreates the table, which would lose every item's Type.
    /// </summary>
    public partial class RenameLayersToTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) => Rename(
            migrationBuilder,
            fromTable: "WorkItemLayers", toTable: "WorkItemTypes",
            fromColumn: "LayerId", toColumn: "TypeId",
            fromField: "layerId", toField: "typeId");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) => Rename(
            migrationBuilder,
            fromTable: "WorkItemTypes", toTable: "WorkItemLayers",
            fromColumn: "TypeId", toColumn: "LayerId",
            fromField: "typeId", toField: "layerId");

        private static void Rename(
            MigrationBuilder migrationBuilder,
            string fromTable,
            string toTable,
            string fromColumn,
            string toColumn,
            string fromField,
            string toField)
        {
            migrationBuilder.DropForeignKey(
                name: $"FK_WorkItems_{fromTable}_{fromColumn}",
                table: "WorkItems");

            migrationBuilder.RenameTable(name: fromTable, newName: toTable);
            migrationBuilder.Sql($"""ALTER TABLE "{toTable}" RENAME CONSTRAINT "PK_{fromTable}" TO "PK_{toTable}";""");
            migrationBuilder.RenameIndex(
                name: $"IX_{fromTable}_Order",
                table: toTable,
                newName: $"IX_{toTable}_Order");

            migrationBuilder.RenameColumn(name: fromColumn, table: "WorkItems", newName: toColumn);
            migrationBuilder.RenameIndex(
                name: $"IX_WorkItems_{fromColumn}",
                table: "WorkItems",
                newName: $"IX_WorkItems_{toColumn}");

            migrationBuilder.AddForeignKey(
                name: $"FK_WorkItems_{toTable}_{toColumn}",
                table: "WorkItems",
                column: toColumn,
                principalTable: toTable,
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // History keeps the field's name, so old entries would otherwise show as an unknown field.
            migrationBuilder.Sql(
                $"""UPDATE "WorkItemHistory" SET "Field" = '{toField}' WHERE "Field" = '{fromField}';""");
        }
    }
}
