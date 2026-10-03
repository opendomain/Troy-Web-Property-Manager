using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <summary>
    /// Timestamps used to be stored on the business's wall clock (BusinessClock.Now), which repeats an hour each
    /// autumn. They're now stored in UTC (BusinessClock.UtcNow) and shown in the business's zone. No columns change;
    /// this converts the rows already there.
    /// </summary>
    /// <remarks>
    /// The existing times were written in the zone from the BusinessTimeZone setting, which ships as America/New_York -
    /// "Eastern Standard Time" to SQL Server's AT TIME ZONE (it covers daylight time too). A database that ran under a
    /// different BusinessTimeZone needs that name changed below before this runs. Lease start and end dates aren't
    /// touched: they're calendar dates on the business's calendar, not moments in time.
    /// </remarks>
    public partial class UtcTimestamps : Migration
    {
        private const string BusinessZone = "Eastern Standard Time";

        private static readonly (string Table, string Column)[] Timestamps =
        [
            ("RentalApplications", "Created"),
            ("RentalApplications", "Submitted"),
            ("RentalApplications", "ReviewClaimed"),
            ("ApplicationStatusHistory", "ChangedDate"),
            ("ApplicationApplicant", "Added"),
            ("ManagerNote", "UpdatedDate")
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Convert(migrationBuilder, from: BusinessZone, to: "UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Convert(migrationBuilder, from: "UTC", to: BusinessZone);
        }

        /// <summary>
        /// Reads each value as a time in <paramref name="from"/> and rewrites it as the same moment in
        /// <paramref name="to"/>. NULLs stay NULL.
        /// </summary>
        private static void Convert(MigrationBuilder migrationBuilder, string from, string to)
        {
            foreach (var (table, column) in Timestamps)
            {
                migrationBuilder.Sql(
                    $"UPDATE [{table}] SET [{column}] = CONVERT(datetime, [{column}] AT TIME ZONE '{from}' AT TIME ZONE '{to}') " +
                    $"WHERE [{column}] IS NOT NULL;");
            }
        }
    }
}
