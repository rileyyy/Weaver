using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TypedLinksAndUrlLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every link before this was untyped "related to". EF generated an empty-string
            // default, which isn't a link type and wouldn't read back; set by hand (see
            // docs/development.md's note on checking a generated column default).
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "WorkItemLinks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Related");

            migrationBuilder.CreateTable(
                name: "WorkItemUrlLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemUrlLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemUrlLinks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemUrlLinks_WorkItemId",
                table: "WorkItemUrlLinks",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemUrlLinks");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "WorkItemLinks");
        }
    }
}
