using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentalApplications_OpenPerApplicantUnit",
                table: "RentalApplications");

            migrationBuilder.DropIndex(
                name: "IX_RentalApplications_Status",
                table: "RentalApplications");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewClaimed",
                table: "RentalApplications",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerUser",
                table: "RentalApplications",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalApplications_OpenPerApplicantUnit",
                table: "RentalApplications",
                columns: new[] { "ApplicantID", "UnitID" },
                unique: true,
                filter: "[Status] IN (1, 2, 3, 7)");

            migrationBuilder.CreateIndex(
                name: "IX_RentalApplications_Status_Submitted",
                table: "RentalApplications",
                columns: new[] { "Status", "Submitted" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_RentalApplications_ReviewClaim",
                table: "RentalApplications",
                sql: "([Status] = 7 AND [ReviewerUser] IS NOT NULL AND [ReviewClaimed] IS NOT NULL) OR ([Status] <> 7 AND [ReviewerUser] IS NULL AND [ReviewClaimed] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Without the claim columns there's no Under Review, so put those applications back in the queue.
            migrationBuilder.Sql("UPDATE [RentalApplications] SET [Status] = 2 WHERE [Status] = 7");

            migrationBuilder.DropIndex(
                name: "IX_RentalApplications_OpenPerApplicantUnit",
                table: "RentalApplications");

            migrationBuilder.DropIndex(
                name: "IX_RentalApplications_Status_Submitted",
                table: "RentalApplications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RentalApplications_ReviewClaim",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "ReviewClaimed",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "ReviewerUser",
                table: "RentalApplications");

            migrationBuilder.CreateIndex(
                name: "IX_RentalApplications_OpenPerApplicantUnit",
                table: "RentalApplications",
                columns: new[] { "ApplicantID", "UnitID" },
                unique: true,
                filter: "[Status] IN (1, 2, 3)");

            migrationBuilder.CreateIndex(
                name: "IX_RentalApplications_Status",
                table: "RentalApplications",
                column: "Status");
        }
    }
}
