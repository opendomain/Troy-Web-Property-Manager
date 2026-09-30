using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <summary>
    /// Renames tables and columns to the terms used in the technical assessment document
    /// (rental application, monthly rent, Applicant Information, Residence History, active unit type).
    /// Hand-written as in-place renames: EF scaffolded a drop-and-recreate of the applications table, which would lose data.
    /// Foreign key constraint names are unchanged; the model still configures them by their original names.
    /// </summary>
    public partial class NormalizeNamesToAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "RentApplications", newName: "RentalApplications");

            // sp_rename changes the key name in place; dropping it would first require dropping the FKs that reference it.
            migrationBuilder.Sql("EXEC sp_rename N'[PK_RentApplications]', N'PK_RentalApplications', N'OBJECT';");

            migrationBuilder.RenameIndex(name: "IX_RentApplications_ApplicantID", table: "RentalApplications", newName: "IX_RentalApplications_ApplicantID");
            migrationBuilder.RenameIndex(name: "IX_RentApplications_Status", table: "RentalApplications", newName: "IX_RentalApplications_Status");
            migrationBuilder.RenameIndex(name: "IX_RentApplications_UnitID", table: "RentalApplications", newName: "IX_RentalApplications_UnitID");

            migrationBuilder.RenameColumn(name: "ApplicantSectionSaved", table: "RentalApplications", newName: "ApplicantInformationSaved");
            migrationBuilder.RenameColumn(name: "ResidenceSectionSaved", table: "RentalApplications", newName: "ResidenceHistorySaved");

            migrationBuilder.RenameColumn(name: "Rent", table: "Unit", newName: "MonthlyRent");
            migrationBuilder.RenameColumn(name: "Active", table: "UnitType", newName: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(name: "IsActive", table: "UnitType", newName: "Active");
            migrationBuilder.RenameColumn(name: "MonthlyRent", table: "Unit", newName: "Rent");

            migrationBuilder.RenameColumn(name: "ResidenceHistorySaved", table: "RentalApplications", newName: "ResidenceSectionSaved");
            migrationBuilder.RenameColumn(name: "ApplicantInformationSaved", table: "RentalApplications", newName: "ApplicantSectionSaved");

            migrationBuilder.RenameIndex(name: "IX_RentalApplications_UnitID", table: "RentalApplications", newName: "IX_RentApplications_UnitID");
            migrationBuilder.RenameIndex(name: "IX_RentalApplications_Status", table: "RentalApplications", newName: "IX_RentApplications_Status");
            migrationBuilder.RenameIndex(name: "IX_RentalApplications_ApplicantID", table: "RentalApplications", newName: "IX_RentApplications_ApplicantID");

            migrationBuilder.Sql("EXEC sp_rename N'[PK_RentalApplications]', N'PK_RentApplications', N'OBJECT';");

            migrationBuilder.RenameTable(name: "RentalApplications", newName: "RentApplications");
        }
    }
}
