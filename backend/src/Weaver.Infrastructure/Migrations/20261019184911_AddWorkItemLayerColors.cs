using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkItemLayerColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "WorkItemLayers",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                // Layers an admin added get a neutral blue-grey; "" isn't a valid color.
                defaultValue: "#546E7A");

            migrationBuilder.UpdateData(
                table: "WorkItemLayers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"),
                column: "Color",
                value: "#D81B60");

            migrationBuilder.UpdateData(
                table: "WorkItemLayers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"),
                column: "Color",
                value: "#00897B");

            migrationBuilder.UpdateData(
                table: "WorkItemLayers",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000103"),
                column: "Color",
                value: "#43A047");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "WorkItemLayers");
        }
    }
}
