using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Troy_Web_Property_Manager.Data.Migrations
{
    /// <inheritdoc />
    public partial class IndexActiveLeaseLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lease_UnitID",
                table: "Lease");

            migrationBuilder.CreateIndex(
                name: "IX_Lease_UnitID_Term",
                table: "Lease",
                columns: new[] { "UnitID", "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Lease_UnitID_Term",
                table: "Lease");

            migrationBuilder.CreateIndex(
                name: "IX_Lease_UnitID",
                table: "Lease",
                column: "UnitID");
        }
    }
}
