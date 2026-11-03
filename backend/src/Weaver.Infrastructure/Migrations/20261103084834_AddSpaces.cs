using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No data migration (ADR-0001: Weaver isn't deployed yet). Existing work items would
            // get the empty Guid as their Space, which the foreign key below rejects, so a
            // database that already has work items must be reset before this runs.
            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ParentId_StatusId_Rank",
                table: "WorkItems");

            migrationBuilder.AddColumn<Guid>(
                name: "SpaceId",
                table: "WorkItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpaceHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApiKeyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Field = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    OldValue = table.Column<string>(type: "jsonb", nullable: true),
                    NewValue = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpaceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpaceHistory_ApiKeys_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpaceHistory_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SpaceHistory_Users_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ParentId",
                table: "WorkItems",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_SpaceId_ParentId_StatusId_Rank",
                table: "WorkItems",
                columns: new[] { "SpaceId", "ParentId", "StatusId", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_SpaceHistory_AccountId",
                table: "SpaceHistory",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceHistory_ApiKeyId",
                table: "SpaceHistory",
                column: "ApiKeyId");

            migrationBuilder.CreateIndex(
                name: "IX_SpaceHistory_SpaceId_Id",
                table: "SpaceHistory",
                columns: new[] { "SpaceId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_Name",
                table: "Spaces",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Spaces_SpaceId",
                table: "WorkItems",
                column: "SpaceId",
                principalTable: "Spaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Spaces_SpaceId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "SpaceHistory");

            migrationBuilder.DropTable(
                name: "Spaces");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_ParentId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_SpaceId_ParentId_StatusId_Rank",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "SpaceId",
                table: "WorkItems");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_ParentId_StatusId_Rank",
                table: "WorkItems",
                columns: new[] { "ParentId", "StatusId", "Rank" });
        }
    }
}
