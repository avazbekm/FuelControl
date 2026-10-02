using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaoshOyiYagona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaoshOyi",
                table: "Harakatlar",
                type: "TEXT",
                nullable: true);

            // Mavjud maosh yozuvlariga oyini qo'yamiz (Sana — oy boshi, UTC) va ikki marta yozilganlarni tozalaymiz:
            // har operator/oy uchun eng kichik Id qoladi.
            migrationBuilder.Sql("UPDATE \"Harakatlar\" SET \"MaoshOyi\" = strftime('%Y-%m', \"Sana\") WHERE \"Turi\" = 'Maosh';");
            migrationBuilder.Sql("""
                DELETE FROM "Harakatlar"
                WHERE "Turi" = 'Maosh'
                  AND "Id" NOT IN (SELECT MIN("Id") FROM "Harakatlar" WHERE "Turi" = 'Maosh' GROUP BY "OperatorId", "MaoshOyi");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Harakatlar_MaoshOyi",
                table: "Harakatlar",
                columns: new[] { "OperatorId", "MaoshOyi" },
                unique: true,
                filter: "\"MaoshOyi\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Harakatlar_MaoshOyi",
                table: "Harakatlar");

            migrationBuilder.DropColumn(
                name: "MaoshOyi",
                table: "Harakatlar");
        }
    }
}
