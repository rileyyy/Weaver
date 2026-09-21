using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF's generator left no default for this NOT NULL column, which
            // would fail against any pre-existing row — explicit empty-array
            // default added by hand (see agent_notes.md's note on always
            // checking a generated column default).
            migrationBuilder.AddColumn<List<string>>(
                name: "Tags",
                table: "WorkItems",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "WorkItems");
        }
    }
}
