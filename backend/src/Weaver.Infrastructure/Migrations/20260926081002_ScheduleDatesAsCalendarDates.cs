using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Weaver.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScheduleDatesAsCalendarDates : Migration
    {
        // Hand-written instead of EF's AlterColumn: a plain timestamptz -> date
        // cast truncates in UTC. The client sent the picked day as *local*
        // midnight converted to UTC (25 Sept in UTC+2 was stored as
        // 2026-09-24T22:00Z), so truncating would keep the one-day-early bug.
        // Rounding to the nearest UTC midnight recovers the picked day for any
        // zone between UTC-12 and UTC+12.
        private const string ToCalendarDate =
            "((\"{0}\" AT TIME ZONE 'UTC') + interval '12 hours')::date";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                ALTER TABLE "WorkItems"
                    ALTER COLUMN "StartDate" TYPE date USING {string.Format(ToCalendarDate, "StartDate")},
                    ALTER COLUMN "EndDate" TYPE date USING {string.Format(ToCalendarDate, "EndDate")};
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Lossless in this direction: each date becomes UTC midnight.
            migrationBuilder.Sql(
                """
                ALTER TABLE "WorkItems"
                    ALTER COLUMN "StartDate" TYPE timestamp with time zone USING ("StartDate"::timestamp AT TIME ZONE 'UTC'),
                    ALTER COLUMN "EndDate" TYPE timestamp with time zone USING ("EndDate"::timestamp AT TIME ZONE 'UTC');
                """);
        }
    }
}
