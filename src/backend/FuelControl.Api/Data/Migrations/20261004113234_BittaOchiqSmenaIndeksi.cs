using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <summary>
    /// Butun shoxobchada bir vaqtda faqat bitta ochiq smena - bazada kafolat: ifodali qisman unikal indeks (EF modeli uni bilmaydi,
    /// shuning uchun alohida migratsiya; ilova ishga tushganda ham BazaKafolati bilan IF NOT EXISTS tekshiriladi).
    /// </summary>
    public partial class BittaOchiqSmenaIndeksi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_Smenalar_BittaOchiq" ON "Smenalar" ((1)) WHERE "Tugadi" IS NULL;""");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Smenalar_BittaOchiq";""");
    }
}
