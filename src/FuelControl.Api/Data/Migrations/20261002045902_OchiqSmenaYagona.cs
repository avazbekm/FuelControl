using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OchiqSmenaYagona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Smenalar_OchiqSmena",
                table: "Smenalar",
                column: "OperatorId",
                unique: true,
                filter: "\"Tugadi\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Smenalar_OchiqSmena",
                table: "Smenalar");
        }
    }
}
