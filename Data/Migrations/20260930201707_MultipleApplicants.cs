using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class MultipleApplicants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ApplicantInformationVersion",
                table: "RentalApplications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ResidenceHistoryVersion",
                table: "RentalApplications",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "ApplicationApplicant",
                columns: table => new
                {
                    RentalApplicationID = table.Column<int>(type: "int", nullable: false),
                    ApplicantID = table.Column<int>(type: "int", nullable: false),
                    Added = table.Column<DateTime>(type: "datetime", nullable: false),
                    AddedByUser = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationApplicant", x => new { x.RentalApplicationID, x.ApplicantID });
                    table.ForeignKey(
                        name: "FK_ApplicationApplicant_Applicant",
                        column: x => x.ApplicantID,
                        principalTable: "Applicant",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_ApplicationApplicant_RentalApplications",
                        column: x => x.RentalApplicationID,
                        principalTable: "RentalApplications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationApplicant_ApplicantID",
                table: "ApplicationApplicant",
                column: "ApplicantID");

            // Every existing application's starter goes on it, so they keep access once ownership is checked through
            // this table. "Added" is when the application was created; the starter added themselves. A profile whose
            // login was deleted has no UserId, so that becomes "".
            migrationBuilder.Sql(
                "INSERT INTO [ApplicationApplicant] ([RentalApplicationID], [ApplicantID], [Added], [AddedByUser]) " +
                "SELECT r.[id], r.[ApplicantID], r.[Created], COALESCE(p.[UserId], N'') " +
                "FROM [RentalApplications] r JOIN [Applicant] p ON p.[id] = r.[ApplicantID]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationApplicant");

            migrationBuilder.DropColumn(
                name: "ApplicantInformationVersion",
                table: "RentalApplications");

            migrationBuilder.DropColumn(
                name: "ResidenceHistoryVersion",
                table: "RentalApplications");
        }
    }
}
