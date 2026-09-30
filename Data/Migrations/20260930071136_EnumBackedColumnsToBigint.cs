using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <summary>
    /// Stores enum-backed values as bigint (the enums are now <c>: long</c>): Status.id and RentalApplications.Status
    /// (ApplicationStatus) and the history's PreviousStatus, NewStatus and Outcome.
    /// Hand-edited: SQL Server won't change a column's type while a primary key, foreign key or index depends on it,
    /// so PK_Status, FK_RentApplications_Status and IX_RentalApplications_Status are dropped and recreated with the same names.
    /// </summary>
    public partial class EnumBackedColumnsToBigint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_RentApplications_Status", table: "RentalApplications");
            migrationBuilder.DropIndex(name: "IX_RentalApplications_Status", table: "RentalApplications");
            migrationBuilder.DropPrimaryKey(name: "PK_Status", table: "Status");

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "Status",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<long>(
                name: "Status",
                table: "RentalApplications",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "PreviousStatus",
                table: "ApplicationStatusHistory",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "NewStatus",
                table: "ApplicationStatusHistory",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<long>(
                name: "Outcome",
                table: "ApplicationStatusHistory",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(name: "PK_Status", table: "Status", column: "id");
            migrationBuilder.CreateIndex(name: "IX_RentalApplications_Status", table: "RentalApplications", column: "Status");
            migrationBuilder.AddForeignKey(
                name: "FK_RentApplications_Status",
                table: "RentalApplications",
                column: "Status",
                principalTable: "Status",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_RentApplications_Status", table: "RentalApplications");
            migrationBuilder.DropIndex(name: "IX_RentalApplications_Status", table: "RentalApplications");
            migrationBuilder.DropPrimaryKey(name: "PK_Status", table: "Status");

            migrationBuilder.AlterColumn<int>(
                name: "Outcome",
                table: "ApplicationStatusHistory",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "NewStatus",
                table: "ApplicationStatusHistory",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "PreviousStatus",
                table: "ApplicationStatusHistory",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "RentalApplications",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "Status",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("SqlServer:Identity", "1, 1")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddPrimaryKey(name: "PK_Status", table: "Status", column: "id");
            migrationBuilder.CreateIndex(name: "IX_RentalApplications_Status", table: "RentalApplications", column: "Status");
            migrationBuilder.AddForeignKey(
                name: "FK_RentApplications_Status",
                table: "RentalApplications",
                column: "Status",
                principalTable: "Status",
                principalColumn: "id");
        }
    }
}
