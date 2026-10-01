using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowSectionsSavedWithErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Residence_Dates",
                table: "Residence");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveOutDate",
                table: "Residence",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveInDate",
                table: "Residence",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Residences saved with errors would break the NOT NULL columns and the check constraint, so patch them
            // first: a missing date takes the other one (or today), and a move-out before move-in becomes the move-in.
            // Only unsubmitted drafts can have these, since errors block Submit.
            migrationBuilder.Sql(
                "UPDATE [Residence] SET " +
                "[MoveInDate] = COALESCE([MoveInDate], [MoveOutDate], CAST(GETDATE() AS date)), " +
                "[MoveOutDate] = COALESCE([MoveOutDate], [MoveInDate], CAST(GETDATE() AS date))");
            migrationBuilder.Sql("UPDATE [Residence] SET [MoveOutDate] = [MoveInDate] WHERE [MoveOutDate] < [MoveInDate]");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveOutDate",
                table: "Residence",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "MoveInDate",
                table: "Residence",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Residence_Dates",
                table: "Residence",
                sql: "[MoveOutDate] >= [MoveInDate]");
        }
    }
}
