using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemHistoryRunId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RunId",
                table: "WorkItemHistory",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemHistory_RunId",
                table: "WorkItemHistory",
                column: "RunId",
                filter: "\"RunId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkItemHistory_RunId",
                table: "WorkItemHistory");

            migrationBuilder.DropColumn(
                name: "RunId",
                table: "WorkItemHistory");
        }
    }
}
