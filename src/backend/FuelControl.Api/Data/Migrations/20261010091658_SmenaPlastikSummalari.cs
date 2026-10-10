using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SmenaPlastikSummalari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlastikSummalari",
                table: "Smenalar",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlastikSummalari",
                table: "Smenalar");
        }
    }
}
