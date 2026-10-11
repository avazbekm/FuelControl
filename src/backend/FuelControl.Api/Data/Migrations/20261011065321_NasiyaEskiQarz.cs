using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NasiyaEskiQarz : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EskiQarz",
                table: "Nasiyalar",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EskiQarz",
                table: "Nasiyalar");
        }
    }
}
