using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <summary>
    /// Mavjud nasiya telefonlari "+998 XX XXX XX XX" ko'rinishiga keltiriladi (model o'zgarmaydi - faqat ma'lumot). Qoida TelefonRaqami.Normallashtir
    /// bilan bir xil: raqam bo'lmagan ajratgichlar olib tashlanadi, 12 raqamli "998..." dan kod tushiriladi, aynan 9 raqam qolsa qayta yoziladi.
    /// Boshqacha (masalan, harfli yoki noto'liq) qiymatlar o'zgartirilmaydi - ma'lumot yo'qolmasin.
    /// </summary>
    public partial class TelefonFormati : Migration
    {
        private static readonly string[] Ajratgichlar = [" ", "-", "+", "(", ")", ".", "char(160)", "char(9)", "char(8211)"];

        /// <summary>Ustundagi qiymatdan ajratgichlarni olib tashlaydigan ifoda (qolgan belgilar o'zgarmaydi).</summary>
        private static string Raqamlar(string ustun) =>
            Ajratgichlar.Aggregate(ustun, (ifoda, a) => $"REPLACE({ifoda}, {(a.StartsWith("char", StringComparison.Ordinal) ? a : $"'{a}'")}, '')");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql($"""
                DROP TABLE IF EXISTS "_TelefonTuzatish";
                CREATE TEMP TABLE "_TelefonTuzatish" AS
                SELECT "Id", CASE WHEN length(d) > 9 AND substr(d, 1, 3) = '998' THEN substr(d, 4) ELSE d END AS m
                FROM (SELECT "Id", {Raqamlar("\"Telefon\"")} AS d FROM "Nasiyalar" WHERE "Telefon" <> '');
                UPDATE "Nasiyalar" SET "Telefon" = (
                    SELECT '+998 ' || substr(m, 1, 2) || ' ' || substr(m, 3, 3) || ' ' || substr(m, 6, 2) || ' ' || substr(m, 8, 2)
                    FROM "_TelefonTuzatish" t WHERE t."Id" = "Nasiyalar"."Id")
                WHERE "Id" IN (SELECT "Id" FROM "_TelefonTuzatish" WHERE length(m) = 9 AND m NOT GLOB '*[^0-9]*');
                DROP TABLE "_TelefonTuzatish";
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eski ko'rinishlarni tiklab bo'lmaydi (qiymatlar normallashtirilgan); "+998 XX XXX XX XX" eski kodlar uchun ham to'g'ri.
        }
    }
}
