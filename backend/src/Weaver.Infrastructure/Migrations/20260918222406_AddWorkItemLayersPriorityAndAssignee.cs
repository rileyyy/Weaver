using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemLayersPriorityAndAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LayerId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "WorkItems",
                type: "integer",
                nullable: false,
                // 1 = WorkItemPriority.Medium, matching the C# property's own
                // default (`= WorkItemPriority.Medium`) — EF's auto-generated
                // default here is otherwise 0 (the enum's first declared
                // member, Low), which back-filled every pre-existing row
                // with the wrong value.
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "WorkItemLayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLayers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "WorkItemLayers",
                columns: new[] { "Id", "Name", "Order" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000101"), "Project", 0 },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "Goal", 1 },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "Task", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_AssignedToUserId",
                table: "WorkItems",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_LayerId",
                table: "WorkItems",
                column: "LayerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLayers_Order",
                table: "WorkItemLayers",
                column: "Order",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Users_AssignedToUserId",
                table: "WorkItems",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkItemLayers_LayerId",
                table: "WorkItems",
                column: "LayerId",
                principalTable: "WorkItemLayers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Users_AssignedToUserId",
                table: "WorkItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkItemLayers_LayerId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "WorkItemLayers");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_AssignedToUserId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_LayerId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "LayerId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "WorkItems");
        }
    }
}
