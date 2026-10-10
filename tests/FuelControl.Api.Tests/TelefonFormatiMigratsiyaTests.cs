using FuelControl.Api.Data;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>TelefonFormati migratsiyasi: mavjud nasiya telefonlari "+998 XX XXX XX XX" ga o'tadi, yaroqsiz qiymatlar o'zgarmaydi.</summary>
public sealed class TelefonFormatiMigratsiyaTests : IDisposable
{
    private const string OldingiMigratsiya = "20261004113234_BittaOchiqSmenaIndeksi";
    private const string Standart = "+998 90 123 45 67";
    private readonly SqliteConnection _ulanish = new("Data Source=:memory:");

    public TelefonFormatiMigratsiyaTests() => _ulanish.Open();

    public void Dispose() => _ulanish.Dispose();

    private FuelControlDbContext Yangi() => new(new DbContextOptionsBuilder<FuelControlDbContext>().UseSqlite(_ulanish).Options);

    /// <summary>Eski (migratsiyadan oldingi) qiymat va migratsiyadan keyin kutilgani; null - o'zgarmaydi.</summary>
    private static readonly (string Eski, string? Yangi)[] Namunalar =
    [
        ("+998901234567", Standart),
        ("998901234567", Standart),
        ("90 123 45 67", Standart),
        ("90-123-45-67", Standart),
        ("90.123.45.67", Standart),
        ("+998 (90) 123-45-67", Standart),
        ("901234567", Standart),
        ("+998" + (char)160 + "901234567", Standart),
        (Standart, Standart),
        ("998123456", "+998 99 812 34 56"),                  // 9 raqamli: kod emas, 99 operatorining raqami
        ("", null),
        ("12345", null),
        ("+998 90 123 45 6", null),                          // 8 raqam
        ("8 90 123 45 67", null),                            // 10 raqam
        ("abc", null),
        ("tel 901234567", null),                             // harfli - qo'lda ko'rib chiqiladi, yo'qolmaydi
        ("+998 90 123 45 67 / 91 000 00 00", null),          // ikkita raqam
    ];

    private async Task<List<string>> Otkaz(IEnumerable<string> eskiTelefonlar)
    {
        await using var db = Yangi();
        await db.GetService<IMigrator>().MigrateAsync(OldingiMigratsiya);
        var op = new Foydalanuvchi { ToliqIsm = "Op", Login = "op", ParolXeshi = "x" };
        db.Foydalanuvchilar.Add(op);
        await db.SaveChangesAsync();
        // Smena SQL bilan: joriy modelda keyingi migratsiyalar qo'shgan ustunlar (masalan, PlastikSummalari) bu eski sxemada hali yo'q.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Smenalar" ("Id","OperatorId","Boshlandi","Tugadi","OchishQaytim","OchishTerminal","OchishDepozit","YopishTerminal","YopishDepozit","SanalganNaqd",
                "Izoh","JamiLitr","Savdo","Plastik","DepozitFarqi","NasiyaJami","QaytganNasiya","XarajatJami","Kutilgan","Farq")
            VALUES (1,1,'2026-10-01 03:00:00','2026-10-01 05:00:00',0,0,0,0,0,0,NULL,'0',0,0,0,0,0,0,0,0);
            """);
        foreach (var tel in eskiTelefonlar)
            db.Nasiyalar.Add(new Nasiya { SmenaId = 1, OperatorId = op.Id, KimYozdi = "Op", MijozIsmi = "Mijoz", Telefon = tel, MashinaRaqami = "",
                Summa = 1000, Muddat = new DateOnly(2026, 10, 20), Yozildi = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();                  // TelefonFormati

        await using var yangi = Yangi();
        return await yangi.Nasiyalar.AsNoTracking().OrderBy(n => n.Id).Select(n => n.Telefon).ToListAsync();
    }

    [Fact]
    public async Task MavjudTelefonlarStandartFormatga_YaroqsizlariOzgarmaydi()
    {
        var natija = await Otkaz(Namunalar.Select(x => x.Eski));

        Assert.Equal(Namunalar.Select(x => x.Yangi ?? x.Eski), natija);
    }

    [Fact]
    public async Task Migratsiya_TelefonRaqamiNormallashtir_BilanBirXilNatija()
    {
        var yaroqli = Namunalar.Where(x => x.Yangi is not null).ToArray();

        var natija = await Otkaz(yaroqli.Select(x => x.Eski));

        Assert.Equal(yaroqli.Select(x => TelefonRaqami.Normallashtir(x.Eski)), natija);
    }

    [Fact]
    public async Task MigratsiyaMavjudMalumotsizBazadaHamIshlaydi()
    {
        await using var db = Yangi();
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Nasiyalar.ToListAsync());
        Assert.Contains("20261006080000_TelefonFormati", await db.Database.GetAppliedMigrationsAsync());
    }
}
