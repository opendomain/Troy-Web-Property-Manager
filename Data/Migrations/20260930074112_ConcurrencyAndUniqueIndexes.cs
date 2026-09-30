using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConcurrencyAndUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Unit_PropertyId",
                table: "Unit");

            migrationBuilder.CreateIndex(
                name: "IX_Unit_PropertyId_UnitNumber",
                table: "Unit",
                columns: new[] { "PropertyId", "UnitNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalApplications_OpenPerApplicantUnit",
                table: "RentalApplications",
                columns: new[] { "ApplicantID", "UnitID" },
                unique: true,
                filter: "[Status] IN (1, 2, 3)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Unit_PropertyId_UnitNumber",
                table: "Unit");

            migrationBuilder.DropIndex(
                name: "IX_RentalApplications_OpenPerApplicantUnit",
                table: "RentalApplications");

            migrationBuilder.CreateIndex(
                name: "IX_Unit_PropertyId",
                table: "Unit",
                column: "PropertyId");
        }
    }
}
