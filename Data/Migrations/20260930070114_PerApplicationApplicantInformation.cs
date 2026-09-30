using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerApplicationApplicantInformation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicantInformation",
                columns: table => new
                {
                    RentalApplicationID = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicantInformation", x => x.RentalApplicationID);
                    table.ForeignKey(
                        name: "FK_ApplicantInformation_RentalApplications",
                        column: x => x.RentalApplicationID,
                        principalTable: "RentalApplications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill: applications whose Applicant Information section was already saved get their own copy
            // of the details that were shared on the Applicant row until now.
            migrationBuilder.Sql(@"
INSERT INTO ApplicantInformation (RentalApplicationID, Name, Phone, Email, CurrentAddress)
SELECT r.id, a.Name, a.Phone, a.Email, a.CurrentAddress
FROM RentalApplications r
JOIN Applicant a ON a.id = r.ApplicantID
WHERE r.ApplicantInformationSaved = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicantInformation");
        }
    }
}
