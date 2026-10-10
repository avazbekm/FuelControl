using FuelControl.Api.Data;
using FuelControl.Api.Xizmatlar;
using FuelControl.Core.Modellar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>SmenaPlastikSummalari migratsiyasi: 0.1.1 bazasi (TelefonFormati sxemasi) ma'lumotini yo'qotmasdan yangi ustun oladi.</summary>
public sealed class SmenaPlastikMigratsiyaTests : IDisposable
{
    private const string Versiya011 = "20261006080000_TelefonFormati";
    private readonly SqliteConnection _ulanish = new("Data Source=:memory:");

    public SmenaPlastikMigratsiyaTests() => _ulanish.Open();

    public void Dispose() => _ulanish.Dispose();

    private FuelControlDbContext Yangi() => new(new DbContextOptionsBuilder<FuelControlDbContext>().UseSqlite(_ulanish).Options);

    private async Task<string> Skalyar(string sql)
    {
        await using var buyruq = _ulanish.CreateCommand();
        buyruq.CommandText = sql;
        return Convert.ToString(await buyruq.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>0.1.1 sxemasidagi baza: bitta yopilgan va bitta ochiq smena (ustun hali yo'q, shuning uchun SQL bilan), so'ng oxirgi migratsiyagacha.</summary>
    private async Task<FuelControlDbContext> Otkaz()
    {
        var db = Yangi();
        await db.GetService<IMigrator>().MigrateAsync(Versiya011);
        Assert.Equal("0", await Skalyar("SELECT COUNT(*) FROM pragma_table_info('Smenalar') WHERE name = 'PlastikSummalari'"));
        db.Foydalanuvchilar.Add(new Foydalanuvchi { ToliqIsm = "Op", Login = "op", ParolXeshi = "x" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Smenalar" ("Id","OperatorId","Boshlandi","Tugadi","OchishQaytim","OchishTerminal","OchishDepozit","YopishTerminal","YopishDepozit","SanalganNaqd",
                "Izoh","JamiLitr","Savdo","Plastik","DepozitFarqi","NasiyaJami","QaytganNasiya","XarajatJami","Kutilgan","Farq") VALUES
              (1,1,'2026-10-01 03:00:00','2026-10-02 03:00:00',100000,200000,1000000,7830000,1500000,4878000,NULL,'1236.80',16468520,7630000,500000,0,0,0,4923520,-45520),
              (2,1,'2026-10-02 03:00:00',NULL,100000,7830000,1500000,NULL,NULL,NULL,NULL,'0',0,0,0,0,0,0,0,0);
            """);
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task Oldingi011Bazasi_YangiUstunBoshQiymat_SmenalarSaqlandi_DtodaBoshMassiv()
    {
        await using var db = await Otkaz();

        Assert.Contains("20261010091658_SmenaPlastikSummalari", await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal("1", await Skalyar("SELECT COUNT(*) FROM pragma_table_info('Smenalar') WHERE name = 'PlastikSummalari' AND \"notnull\" = 1 AND dflt_value = char(39) || char(39)"));
        var smenalar = await db.Smenalar.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, smenalar.Count);
        Assert.Equal((7_830_000L, 7_630_000L, -45_520L), (smenalar[0].YopishTerminal, smenalar[0].Plastik, smenalar[0].Farq));   // eski ma'lumot o'zgarmagan
        Assert.All(smenalar, s => Assert.Empty(s.PlastikSummalari));
        Assert.All(smenalar, s => Assert.Empty(s.Dto("Op").PlastikSummalari));                                                     // SmenaDto: eski smenalarda []
    }

    [Fact]
    public async Task MigratsiyadanKeyin_RoyxatYoziladiVaQaytaOqiladi_BazadaVergulBilanAjratilgan()
    {
        await using var db = await Otkaz();
        var smena = await db.Smenalar.SingleAsync(x => x.Id == 1);

        smena.PlastikSummalari = [7_000_000, 830_000];
        await db.SaveChangesAsync();

        Assert.Equal("7000000,830000", await Skalyar("""SELECT "PlastikSummalari" FROM "Smenalar" WHERE "Id" = 1"""));
        Assert.Equal("", await Skalyar("""SELECT "PlastikSummalari" FROM "Smenalar" WHERE "Id" = 2"""));
        await using var yangi = Yangi();
        var oqildi = await yangi.Smenalar.AsNoTracking().SingleAsync(x => x.Id == 1);
        Assert.Equal([7_000_000L, 830_000L], oqildi.PlastikSummalari);
        Assert.Equal([7_000_000L, 830_000L], oqildi.Dto("Op").PlastikSummalari);
    }

    [Fact]
    public void PlastikMatni_YozishVaOqish_YaroqsizBolaklarOtkazib_YuboriladiBoshMatnBoshMassiv()
    {
        Assert.Equal("7830000,300000,0", PlastikMatni.Yoz([7_830_000, 300_000, 0]));
        Assert.Equal("", PlastikMatni.Yoz([]));
        Assert.Equal([7_830_000L, 300_000L, 0L], PlastikMatni.Oqi("7830000,300000,0"));
        Assert.Empty(PlastikMatni.Oqi(""));
        Assert.Empty(PlastikMatni.Oqi(null));
        Assert.Equal([7_830_000L, 12L, 0L], PlastikMatni.Oqi(" 7830000 ,,x, -5 ,12,0, 1.5 "));
    }
}
