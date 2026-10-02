using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelControl.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Boshlangich : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Audit",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Kim = table.Column<string>(type: "TEXT", nullable: false),
                    Amal = table.Column<string>(type: "TEXT", nullable: false),
                    Tafsilot = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Audit", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Foydalanuvchilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ToliqIsm = table.Column<string>(type: "TEXT", nullable: false),
                    Login = table.Column<string>(type: "TEXT", nullable: false),
                    Rol = table.Column<string>(type: "TEXT", nullable: false),
                    Faol = table.Column<bool>(type: "INTEGER", nullable: false),
                    OylikMaosh = table.Column<long>(type: "INTEGER", nullable: false),
                    ParolXeshi = table.Column<string>(type: "TEXT", nullable: false),
                    Ruxsatlar = table.Column<string>(type: "TEXT", nullable: false),
                    XatoUrinishlar = table.Column<int>(type: "INTEGER", nullable: false),
                    BlokGacha = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Foydalanuvchilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NarxTarixlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    YoqilgiTuriId = table.Column<int>(type: "INTEGER", nullable: false),
                    YoqilgiNomi = table.Column<string>(type: "TEXT", nullable: false),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EskiNarx = table.Column<long>(type: "INTEGER", nullable: false),
                    YangiNarx = table.Column<long>(type: "INTEGER", nullable: false),
                    Kim = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NarxTarixlari", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Yoqilgilar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nomi = table.Column<string>(type: "TEXT", nullable: false),
                    Narx = table.Column<long>(type: "INTEGER", nullable: false),
                    Rang = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Yoqilgilar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Harakatlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OperatorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sana = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Turi = table.Column<string>(type: "TEXT", nullable: false),
                    Summa = table.Column<long>(type: "INTEGER", nullable: false),
                    Izoh = table.Column<string>(type: "TEXT", nullable: false),
                    KimYozdi = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Harakatlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Harakatlar_Foydalanuvchilar_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Foydalanuvchilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Smenalar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OperatorId = table.Column<int>(type: "INTEGER", nullable: false),
                    Boshlandi = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tugadi = table.Column<DateTime>(type: "TEXT", nullable: true),
                    KutilganNaqd = table.Column<long>(type: "INTEGER", nullable: false),
                    KutilganPlastik = table.Column<long>(type: "INTEGER", nullable: false),
                    KutilganClick = table.Column<long>(type: "INTEGER", nullable: false),
                    JamiLitr = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    SotuvSoni = table.Column<int>(type: "INTEGER", nullable: false),
                    TopshirilganNaqd = table.Column<long>(type: "INTEGER", nullable: true),
                    TopshirilganPlastik = table.Column<long>(type: "INTEGER", nullable: true),
                    TopshirilganClick = table.Column<long>(type: "INTEGER", nullable: true),
                    Izoh = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Smenalar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Smenalar_Foydalanuvchilar_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Foydalanuvchilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Aparatlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Raqam = table.Column<int>(type: "INTEGER", nullable: false),
                    YoqilgiTuriId = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalLitr = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aparatlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aparatlar_Yoqilgilar_YoqilgiTuriId",
                        column: x => x.YoqilgiTuriId,
                        principalTable: "Yoqilgilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sotuvlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SmenaId = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatorId = table.Column<int>(type: "INTEGER", nullable: false),
                    AparatId = table.Column<int>(type: "INTEGER", nullable: false),
                    Narx = table.Column<long>(type: "INTEGER", nullable: false),
                    Litr = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Summa = table.Column<long>(type: "INTEGER", nullable: false),
                    Vaqt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Holati = table.Column<string>(type: "TEXT", nullable: false),
                    BekorSababi = table.Column<string>(type: "TEXT", nullable: true),
                    BekorQilgan = table.Column<string>(type: "TEXT", nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sotuvlar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sotuvlar_Aparatlar_AparatId",
                        column: x => x.AparatId,
                        principalTable: "Aparatlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sotuvlar_Foydalanuvchilar_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "Foydalanuvchilar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sotuvlar_Smenalar_SmenaId",
                        column: x => x.SmenaId,
                        principalTable: "Smenalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SotuvTolovlari",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SotuvId = table.Column<int>(type: "INTEGER", nullable: false),
                    Turi = table.Column<string>(type: "TEXT", nullable: false),
                    Summa = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SotuvTolovlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SotuvTolovlari_Sotuvlar_SotuvId",
                        column: x => x.SotuvId,
                        principalTable: "Sotuvlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aparatlar_Raqam",
                table: "Aparatlar",
                column: "Raqam",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aparatlar_YoqilgiTuriId",
                table: "Aparatlar",
                column: "YoqilgiTuriId");

            migrationBuilder.CreateIndex(
                name: "IX_Audit_Vaqt",
                table: "Audit",
                column: "Vaqt");

            migrationBuilder.CreateIndex(
                name: "IX_Foydalanuvchilar_Login",
                table: "Foydalanuvchilar",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Harakatlar_OperatorId_Sana",
                table: "Harakatlar",
                columns: new[] { "OperatorId", "Sana" });

            migrationBuilder.CreateIndex(
                name: "IX_NarxTarixlari_Vaqt",
                table: "NarxTarixlari",
                column: "Vaqt");

            migrationBuilder.CreateIndex(
                name: "IX_Smenalar_Boshlandi",
                table: "Smenalar",
                column: "Boshlandi");

            migrationBuilder.CreateIndex(
                name: "IX_Smenalar_OperatorId_Tugadi",
                table: "Smenalar",
                columns: new[] { "OperatorId", "Tugadi" });

            migrationBuilder.CreateIndex(
                name: "IX_Sotuvlar_AparatId",
                table: "Sotuvlar",
                column: "AparatId");

            migrationBuilder.CreateIndex(
                name: "IX_Sotuvlar_IdempotencyKey",
                table: "Sotuvlar",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sotuvlar_OperatorId",
                table: "Sotuvlar",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Sotuvlar_SmenaId",
                table: "Sotuvlar",
                column: "SmenaId");

            migrationBuilder.CreateIndex(
                name: "IX_Sotuvlar_Vaqt",
                table: "Sotuvlar",
                column: "Vaqt");

            migrationBuilder.CreateIndex(
                name: "IX_SotuvTolovlari_SotuvId",
                table: "SotuvTolovlari",
                column: "SotuvId");

            migrationBuilder.CreateIndex(
                name: "IX_Yoqilgilar_Nomi",
                table: "Yoqilgilar",
                column: "Nomi",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Audit");

            migrationBuilder.DropTable(
                name: "Harakatlar");

            migrationBuilder.DropTable(
                name: "NarxTarixlari");

            migrationBuilder.DropTable(
                name: "SotuvTolovlari");

            migrationBuilder.DropTable(
                name: "Sotuvlar");

            migrationBuilder.DropTable(
                name: "Aparatlar");

            migrationBuilder.DropTable(
                name: "Smenalar");

            migrationBuilder.DropTable(
                name: "Yoqilgilar");

            migrationBuilder.DropTable(
                name: "Foydalanuvchilar");
        }
    }
}
