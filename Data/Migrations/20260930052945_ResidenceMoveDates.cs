using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResidenceMoveDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename and convert in place (datetime -> date) so existing residence dates are kept.
            migrationBuilder.RenameColumn(
                name: "StartDate",
                table: "Residence",
                newName: "MoveInDate");

            migrationBuilder.RenameColumn(
                name: "EndDate",
                table: "Residence",
                newName: "MoveOutDate");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveInDate",
                table: "Residence",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveOutDate",
                table: "Residence",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "MoveInDate",
                table: "Residence",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateTime>(
                name: "MoveOutDate",
                table: "Residence",
                type: "datetime",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.RenameColumn(
                name: "MoveInDate",
                table: "Residence",
                newName: "StartDate");

            migrationBuilder.RenameColumn(
                name: "MoveOutDate",
                table: "Residence",
                newName: "EndDate");
        }
    }
}
