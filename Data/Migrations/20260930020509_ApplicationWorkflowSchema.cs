using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationWorkflowSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Rent",
                table: "Unit",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(float),
                oldType: "real",
                oldDefaultValue: 0f)
                .Annotation("Relational:DefaultConstraintName", "DF_Unit_Rent")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_Unit_Rent");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Submitted",
                table: "RentApplications",
                type: "datetime",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AddColumn<bool>(
                name: "ApplicantSectionSaved",
                table: "RentApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ResidenceSectionSaved",
                table: "RentApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "ApplicationStatusHistory",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ChangedByUser",
                table: "ApplicationStatusHistory",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Applicant",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applicant_UserId",
                table: "Applicant",
                column: "UserId",
                unique: true,
                filter: "[UserId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Applicant_AspNetUsers",
                table: "Applicant",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applicant_AspNetUsers",
                table: "Applicant");

            migrationBuilder.DropIndex(
                name: "IX_Applicant_UserId",
                table: "Applicant");

            migrationBuilder.DropColumn(
                name: "ApplicantSectionSaved",
                table: "RentApplications");

            migrationBuilder.DropColumn(
                name: "ResidenceSectionSaved",
                table: "RentApplications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Applicant");

            migrationBuilder.AlterColumn<float>(
                name: "Rent",
                table: "Unit",
                type: "real",
                nullable: false,
                defaultValue: 0f,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldDefaultValue: 0m)
                .Annotation("Relational:DefaultConstraintName", "DF_Unit_Rent")
                .OldAnnotation("Relational:DefaultConstraintName", "DF_Unit_Rent");

            migrationBuilder.AlterColumn<DateTime>(
                name: "Submitted",
                table: "RentApplications",
                type: "datetime",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "ApplicationStatusHistory",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<int>(
                name: "ChangedByUser",
                table: "ApplicationStatusHistory",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);
        }
    }
}
