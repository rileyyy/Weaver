using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletionDateAndDoneHiding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "CompletionDate",
                table: "WorkItems",
                type: "date",
                nullable: true);

            // The generator backfills 0, which the domain rejects; existing Spaces get the default period.
            migrationBuilder.AddColumn<int>(
                name: "DoneHidingDays",
                table: "Spaces",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            // Items already Done have no record of when they finished. Their last update is the
            // closest guess, and dating them at all lets the Board hide old ones instead of showing
            // them forever. Category 2 is StatusCategory.Done, stored as its ordinal. The date is
            // the one in the server time zone, as IServerCalendar would have given it.
            migrationBuilder.Sql("""
                UPDATE "WorkItems" w
                SET "CompletionDate" = (w."UpdatedAtUtc" AT TIME ZONE COALESCE(
                    (SELECT "TimeZoneId" FROM "ServerSettings" WHERE "Id" = 1), 'UTC'))::date
                FROM "Statuses" s
                WHERE s."Id" = w."StatusId" AND s."Category" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionDate",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DoneHidingDays",
                table: "Spaces");
        }
    }
}
