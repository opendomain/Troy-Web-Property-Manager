using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyManagementSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Applicant",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CurrentAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applicant", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Property",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Property", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Status",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Status", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "UnitType",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Active = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                        .Annotation("Relational:DefaultConstraintName", "DF_UnitType_Active")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitType", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Unit",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Bedrooms = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                        .Annotation("Relational:DefaultConstraintName", "DF_Unit_Bedrooms"),
                    Rent = table.Column<float>(type: "real", nullable: false, defaultValue: 0f)
                        .Annotation("Relational:DefaultConstraintName", "DF_Unit_Rent"),
                    UnitTypeID = table.Column<int>(type: "int", nullable: false),
                    PropertyId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unit", x => x.id);
                    table.ForeignKey(
                        name: "FK_Unit_Property",
                        column: x => x.PropertyId,
                        principalTable: "Property",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Unit_UnitType",
                        column: x => x.UnitTypeID,
                        principalTable: "UnitType",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "RentApplications",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitID = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime", nullable: false),
                    Submitted = table.Column<DateTime>(type: "datetime", nullable: false),
                    ApplicantID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentApplications", x => x.id);
                    table.ForeignKey(
                        name: "FK_RentApplications_Applicant",
                        column: x => x.ApplicantID,
                        principalTable: "Applicant",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_RentApplications_Status",
                        column: x => x.Status,
                        principalTable: "Status",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_RentApplications_Unit",
                        column: x => x.UnitID,
                        principalTable: "Unit",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ApplicationStatusHistory",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PreviousStatus = table.Column<int>(type: "int", nullable: false),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangedDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    ChangedByUser = table.Column<int>(type: "int", nullable: false),
                    RentalApplicationID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationStatusHistory", x => x.id);
                    table.ForeignKey(
                        name: "FK_ApplicationStatusHistory_RentApplications",
                        column: x => x.RentalApplicationID,
                        principalTable: "RentApplications",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Lease",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UnitID = table.Column<int>(type: "int", nullable: false),
                    RentalApplicationID = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lease", x => x.id);
                    table.ForeignKey(
                        name: "FK_Lease_RentApplications",
                        column: x => x.RentalApplicationID,
                        principalTable: "RentApplications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_Lease_Unit",
                        column: x => x.UnitID,
                        principalTable: "Unit",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "Residence",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LandlordName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LandlordPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime", nullable: false),
                    RentalApplicationID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Residence", x => x.id);
                    table.ForeignKey(
                        name: "FK_Residence_RentApplications",
                        column: x => x.RentalApplicationID,
                        principalTable: "RentApplications",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationStatusHistory_RentalApplicationID",
                table: "ApplicationStatusHistory",
                column: "RentalApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_Lease_RentalApplicationID",
                table: "Lease",
                column: "RentalApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_Lease_UnitID",
                table: "Lease",
                column: "UnitID");

            migrationBuilder.CreateIndex(
                name: "IX_RentApplications_ApplicantID",
                table: "RentApplications",
                column: "ApplicantID");

            migrationBuilder.CreateIndex(
                name: "IX_RentApplications_Status",
                table: "RentApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RentApplications_UnitID",
                table: "RentApplications",
                column: "UnitID");

            migrationBuilder.CreateIndex(
                name: "IX_Residence_RentalApplicationID",
                table: "Residence",
                column: "RentalApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_Unit_PropertyId",
                table: "Unit",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_Unit_UnitTypeID",
                table: "Unit",
                column: "UnitTypeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationStatusHistory");

            migrationBuilder.DropTable(
                name: "Lease");

            migrationBuilder.DropTable(
                name: "Residence");

            migrationBuilder.DropTable(
                name: "RentApplications");

            migrationBuilder.DropTable(
                name: "Applicant");

            migrationBuilder.DropTable(
                name: "Status");

            migrationBuilder.DropTable(
                name: "Unit");

            migrationBuilder.DropTable(
                name: "Property");

            migrationBuilder.DropTable(
                name: "UnitType");
        }
    }
}
