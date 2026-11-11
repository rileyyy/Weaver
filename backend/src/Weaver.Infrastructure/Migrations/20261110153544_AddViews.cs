using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Views",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SpaceIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Tags = table.Column<string[]>(type: "text[]", nullable: false),
                    TagMatch = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ApplyDoneHiding = table.Column<bool>(type: "boolean", nullable: false),
                    ScheduledOrInProgressOnly = table.Column<bool>(type: "boolean", nullable: false),
                    Layout = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Views", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DefaultViews",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DefaultViews", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_DefaultViews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DefaultViews_Views_ViewId",
                        column: x => x.ViewId,
                        principalTable: "Views",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DefaultViews_ViewId",
                table: "DefaultViews",
                column: "ViewId");

            migrationBuilder.CreateIndex(
                name: "IX_Views_Name",
                table: "Views",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DefaultViews");

            migrationBuilder.DropTable(
                name: "Views");
        }
    }
}
