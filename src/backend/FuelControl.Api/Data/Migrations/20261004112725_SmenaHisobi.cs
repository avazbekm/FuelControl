using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <summary>
    /// Sotuv modelidan smena hisobiga o'tish. O'rnatuvchi 0.0.1 bazasi (eski sotuvlar, smena ustunlari, eski ruxsat nomlari) ham to'g'ri o'tadi:
    /// foydalanuvchilar, yoqilg'i, aparatlar, operator hisoblari va audit saqlanadi; eski smenalar tarix sifatida yangi shaklga o'tkaziladi
    /// (ko'rsatkichsiz); sotuvlar o'chadi (sotuv kiritish yo'qoldi). Orqaga qaytarib bo'lmaydi.
    /// </summary>
    public partial class SmenaHisobi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Eski ochiq smenalar (har operatorga bittadan bo'lishi mumkin edi) yopiladi: yangi modelda ochiq smena ochish qoldiqlarisiz bo'lmaydi,
            //    butun shoxobchada esa faqat bitta ochiq smena bo'la oladi.
            migrationBuilder.Sql("""
                UPDATE "Smenalar"
                SET "Tugadi" = COALESCE((SELECT MAX("Vaqt") FROM "Sotuvlar" WHERE "SmenaId" = "Smenalar"."Id"), "Boshlandi")
                WHERE "Tugadi" IS NULL;
                """);

            migrationBuilder.DropIndex(name: "IX_Smenalar_OchiqSmena", table: "Smenalar");

            // 2) Smenalar: yangi ustunlar.
            foreach (var ustun in new[] { "DepozitFarqi", "Farq", "Kutilgan", "NasiyaJami", "OchishDepozit", "OchishQaytim", "OchishTerminal",
                                          "Plastik", "QaytganNasiya", "Savdo", "XarajatJami" })
                migrationBuilder.AddColumn<long>(name: ustun, table: "Smenalar", type: "INTEGER", nullable: false, defaultValue: 0L);
            foreach (var ustun in new[] { "SanalganNaqd", "YopishDepozit", "YopishTerminal" })
                migrationBuilder.AddColumn<long>(name: ustun, table: "Smenalar", type: "INTEGER", nullable: true);

            // 3) Eski smenalarni yangi shaklga o'tkazamiz (ko'rsatkichlari yo'q - faqat tarix): Savdo = eski kutilgan jami, Plastik = eski kutilgan plastik,
            //    DepozitFarqi = eski Click, Farq = eski jami farq (operator hisobidagi kamomat/ortiqcha harakatlari bilan mos), Kutilgan = Sanalgan - Farq.
            migrationBuilder.Sql("""
                UPDATE "Smenalar" SET
                    "Savdo" = "KutilganNaqd" + "KutilganPlastik" + "KutilganClick",
                    "Plastik" = "KutilganPlastik",
                    "DepozitFarqi" = "KutilganClick",
                    "YopishTerminal" = "KutilganPlastik",
                    "YopishDepozit" = "KutilganClick",
                    "SanalganNaqd" = COALESCE("TopshirilganNaqd", "KutilganNaqd"),
                    "Farq" = CASE WHEN "TopshirilganNaqd" IS NULL THEN 0
                                  ELSE COALESCE("TopshirilganNaqd", 0) + COALESCE("TopshirilganPlastik", 0) + COALESCE("TopshirilganClick", 0)
                                       - ("KutilganNaqd" + "KutilganPlastik" + "KutilganClick") END;
                UPDATE "Smenalar" SET "Kutilgan" = "SanalganNaqd" - "Farq";
                """);

            // 4) Sotuv jadvallari va eski smena ustunlari olib tashlanadi.
            migrationBuilder.DropTable(name: "SotuvTolovlari");
            migrationBuilder.DropTable(name: "Sotuvlar");
            foreach (var ustun in new[] { "KutilganNaqd", "KutilganPlastik", "KutilganClick", "SotuvSoni",
                                          "TopshirilganNaqd", "TopshirilganPlastik", "TopshirilganClick" })
                migrationBuilder.DropColumn(name: ustun, table: "Smenalar");

            // 5) Aparat: bak qoldig'i (mavjud aparatlarda 0 - Sozlamalarda o'lchov bo'yicha kiritiladi).
            migrationBuilder.AddColumn<decimal>(name: "BakQoldiq", table: "Aparatlar", type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m);

            // 6) Audit turi: eski yozuvlar amal nomiga qarab.
            migrationBuilder.AddColumn<string>(name: "Tur", table: "Audit", type: "TEXT", nullable: false, defaultValue: "sozlama");
            migrationBuilder.Sql("""
                UPDATE "Audit" SET "Tur" = CASE
                    WHEN "Amal" LIKE 'Smena %' THEN 'smena'
                    WHEN "Amal" LIKE 'Sotuv %' THEN 'tuzatish'
                    WHEN "Amal" IN ('Avans berildi', 'To''lov berildi') THEN 'hisob'
                    WHEN "Amal" LIKE 'Eksport:%Hisob-varaqa%' THEN 'hisob'
                    ELSE 'sozlama' END;
                """);

            // 7) Ruxsatlar: SotuvKiritish -> Savdo; SotuvTahrirlash/SotuvBekorQilish -> KorsatkichTuzatish; yangi standartlar rol bo'yicha qo'shiladi.
            migrationBuilder.Sql("""UPDATE "Foydalanuvchilar" SET "Ruxsatlar" = ',' || "Ruxsatlar" || ',';""");
            migrationBuilder.Sql("""UPDATE "Foydalanuvchilar" SET "Ruxsatlar" = REPLACE("Ruxsatlar", ',SotuvKiritish,', ',Savdo,');""");
            migrationBuilder.Sql("""UPDATE "Foydalanuvchilar" SET "Ruxsatlar" = REPLACE("Ruxsatlar", ',SotuvTahrirlash,', ',KorsatkichTuzatish,');""");
            migrationBuilder.Sql("""UPDATE "Foydalanuvchilar" SET "Ruxsatlar" = REPLACE("Ruxsatlar", ',SotuvBekorQilish,', ',KorsatkichTuzatish,');""");
            migrationBuilder.Sql("""UPDATE "Foydalanuvchilar" SET "Ruxsatlar" = REPLACE("Ruxsatlar", ',KorsatkichTuzatish,KorsatkichTuzatish,', ',KorsatkichTuzatish,');""");
            void Qosh(string rol, params string[] ruxsatlar)
            {
                foreach (var r in ruxsatlar)
                    migrationBuilder.Sql($"""
                        UPDATE "Foydalanuvchilar" SET "Ruxsatlar" = "Ruxsatlar" || '{r},'
                        WHERE "Rol" = '{rol}' AND "Ruxsatlar" NOT LIKE '%,{r},%';
                        """);
            }
            Qosh("Operator", "Nasiyalar", "NasiyaYozish", "QarzQaytdi", "XarajatYozish");
            Qosh("Boshliq", "Nasiyalar", "NasiyaYozish", "QarzQaytdi", "XarajatYozish", "BakKirim");
            Qosh("Admin", "Nasiyalar", "NasiyaYozish", "QarzQaytdi", "XarajatYozish", "BakKirim", "KorsatkichTuzatish");
            migrationBuilder.Sql("""UPDATE "Foydalanuvchilar" SET "Ruxsatlar" = TRIM("Ruxsatlar", ',');""");

            // 8) Yangi jadvallar va indekslar.
            migrationBuilder.CreateTable(
                name: "BakKirimlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AparatId = table.Column<int>(type: "INTEGER", nullable: false),
                    Litr = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    QoldiqOldin = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    QoldiqKeyin = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Hujjat = table.Column<string>(type: "TEXT", nullable: true),
                    KimYozdi = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BakKirimlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BakKirimlari_Aparatlar_AparatId",
                        column: x => x.AparatId,
                        principalTable: "Aparatlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BakTuzatishlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AparatId = table.Column<int>(type: "INTEGER", nullable: false),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Oldin = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Keyin = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Sabab = table.Column<string>(type: "TEXT", nullable: false),
                    KimYozdi = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BakTuzatishlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BakTuzatishlari_Aparatlar_AparatId",
                        column: x => x.AparatId,
                        principalTable: "Aparatlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Nasiyalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SmenaId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatorId = table.Column<int>(type: "INTEGER", nullable: false),
                    KimYozdi = table.Column<string>(type: "TEXT", nullable: false),
                    MijozIsmi = table.Column<string>(type: "TEXT", nullable: false),
                    Telefon = table.Column<string>(type: "TEXT", nullable: false),
                    MashinaRaqami = table.Column<string>(type: "TEXT", nullable: false),
                    Summa = table.Column<long>(type: "INTEGER", nullable: false),
                    Qaytgan = table.Column<long>(type: "INTEGER", nullable: false),
                    Muddat = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Yozildi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Yopildi = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Izoh = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nasiyalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Nasiyalar_Foydalanuvchilar_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Foydalanuvchilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Nasiyalar_Smenalar_SmenaId",
                        column: x => x.SmenaId,
                        principalTable: "Smenalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SmenaKorsatkichlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SmenaId = table.Column<int>(type: "INTEGER", nullable: false),
                    AparatId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tartib = table.Column<int>(type: "INTEGER", nullable: false),
                    Boshi = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Oxiri = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Narx = table.Column<long>(type: "INTEGER", nullable: false),
                    Litr = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Summa = table.Column<long>(type: "INTEGER", nullable: false),
                    NarxOzgarishida = table.Column<bool>(type: "INTEGER", nullable: false),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmenaKorsatkichlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SmenaKorsatkichlari_Aparatlar_AparatId",
                        column: x => x.AparatId,
                        principalTable: "Aparatlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SmenaKorsatkichlari_Smenalar_SmenaId",
                        column: x => x.SmenaId,
                        principalTable: "Smenalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Xarajatlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SmenaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Summa = table.Column<long>(type: "INTEGER", nullable: false),
                    Sabab = table.Column<string>(type: "TEXT", nullable: false),
                    Manba = table.Column<string>(type: "TEXT", nullable: false),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<int>(type: "INTEGER", nullable: false),
                    KimYozdi = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Xarajatlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Xarajatlar_Foydalanuvchilar_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Foydalanuvchilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Xarajatlar_Smenalar_SmenaId",
                        column: x => x.SmenaId,
                        principalTable: "Smenalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NasiyaQaytishlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NasiyaId = table.Column<int>(type: "INTEGER", nullable: false),
                    SmenaId = table.Column<int>(type: "INTEGER", nullable: true),
                    Summa = table.Column<long>(type: "INTEGER", nullable: false),
                    Usul = table.Column<string>(type: "TEXT", nullable: false),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OperatorId = table.Column<int>(type: "INTEGER", nullable: false),
                    KimYozdi = table.Column<string>(type: "TEXT", nullable: false),
                    Izoh = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NasiyaQaytishlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NasiyaQaytishlari_Foydalanuvchilar_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Foydalanuvchilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NasiyaQaytishlari_Nasiyalar_NasiyaId",
                        column: x => x.NasiyaId,
                        principalTable: "Nasiyalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NasiyaQaytishlari_Smenalar_SmenaId",
                        column: x => x.SmenaId,
                        principalTable: "Smenalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Tur",
                table: "Audit",
                column: "Tur");

            migrationBuilder.CreateIndex(
                name: "IX_BakKirimlari_AparatId_Vaqt",
                table: "BakKirimlari",
                columns: new[] { "AparatId", "Vaqt" });

            migrationBuilder.CreateIndex(
                name: "IX_BakTuzatishlari_AparatId_Vaqt",
                table: "BakTuzatishlari",
                columns: new[] { "AparatId", "Vaqt" });

            migrationBuilder.CreateIndex(
                name: "IX_Nasiyalar_OperatorId",
                table: "Nasiyalar",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Nasiyalar_SmenaId",
                table: "Nasiyalar",
                column: "SmenaId");

            migrationBuilder.CreateIndex(
                name: "IX_Nasiyalar_Yozildi",
                table: "Nasiyalar",
                column: "Yozildi");

            migrationBuilder.CreateIndex(
                name: "IX_NasiyaQaytishlari_NasiyaId",
                table: "NasiyaQaytishlari",
                column: "NasiyaId");

            migrationBuilder.CreateIndex(
                name: "IX_NasiyaQaytishlari_OperatorId",
                table: "NasiyaQaytishlari",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_NasiyaQaytishlari_SmenaId",
                table: "NasiyaQaytishlari",
                column: "SmenaId");

            migrationBuilder.CreateIndex(
                name: "IX_NasiyaQaytishlari_Vaqt",
                table: "NasiyaQaytishlari",
                column: "Vaqt");

            migrationBuilder.CreateIndex(
                name: "IX_SmenaKorsatkichlari_AparatId",
                table: "SmenaKorsatkichlari",
                column: "AparatId");

            migrationBuilder.CreateIndex(
                name: "IX_SmenaKorsatkichlari_SmenaId_AparatId_Tartib",
                table: "SmenaKorsatkichlari",
                columns: new[] { "SmenaId", "AparatId", "Tartib" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Xarajatlar_OperatorId",
                table: "Xarajatlar",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Xarajatlar_SmenaId",
                table: "Xarajatlar",
                column: "SmenaId");
            // Bitta ochiq smena indeksi keyingi migratsiyada (BittaOchiqSmenaIndeksi): SQLite jadvalni qayta qurganda (DropColumn) EF modeli bilmagan
            // indeks yo'qoladi, qayta qurish esa migratsiya oxirida bajariladi.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Smena hisobi migratsiyasini orqaga qaytarib bo'lmaydi (sotuvlar o'chirilgan). Zaxira nusxadan tiklang.");
    }
}
