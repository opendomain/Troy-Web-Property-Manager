using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Unit_Bedrooms",
                table: "Unit",
                sql: "[Bedrooms] >= 0 AND [Bedrooms] <= 10");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Residence_Dates",
                table: "Residence",
                sql: "[MoveOutDate] >= [MoveInDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Lease_Term",
                table: "Lease",
                sql: "[EndDate] > [StartDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Unit_Bedrooms",
                table: "Unit");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Residence_Dates",
                table: "Residence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Lease_Term",
                table: "Lease");
        }
    }
}
