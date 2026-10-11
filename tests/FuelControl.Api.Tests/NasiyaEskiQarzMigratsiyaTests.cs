using FuelControl.Api.Data;
using FuelControl.Api.Xizmatlar;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>NasiyaEskiQarz migratsiyasi: 0.1.2 bazasi (SmenaPlastikSummalari sxemasi) nasiya va qaytishlarini yo'qotmasdan EskiQarz ustunini oladi.</summary>
public sealed class NasiyaEskiQarzMigratsiyaTests : IDisposable
{
    private const string Versiya012 = "20261010091658_SmenaPlastikSummalari";
    private readonly SqliteConnection _ulanish = new("Data Source=:memory:");

    public NasiyaEskiQarzMigratsiyaTests() => _ulanish.Open();

    public void Dispose() => _ulanish.Dispose();

    private FuelControlDbContext Yangi() => new(new DbContextOptionsBuilder<FuelControlDbContext>().UseSqlite(_ulanish).Options);

    private async Task<string> Skalyar(string sql)
    {
        await using var buyruq = _ulanish.CreateCommand();
        buyruq.CommandText = sql;
        return Convert.ToString(await buyruq.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>0.1.2 sxemasidagi baza: ochiq smena, bitta faol va bitta yopilgan nasiya (+ qaytishi); ustun hali yo'q, shuning uchun SQL bilan.</summary>
    private async Task<FuelControlDbContext> Otkaz()
    {
        var db = Yangi();
        await db.GetService<IMigrator>().MigrateAsync(Versiya012);
        Assert.Equal("0", await Skalyar("SELECT COUNT(*) FROM pragma_table_info('Nasiyalar') WHERE name = 'EskiQarz'"));
        db.Foydalanuvchilar.Add(new Foydalanuvchi { ToliqIsm = "Op", Login = "op", ParolXeshi = "x" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Smenalar" ("Id","OperatorId","Boshlandi","Tugadi","OchishQaytim","OchishTerminal","OchishDepozit","YopishTerminal","YopishDepozit","SanalganNaqd",
                "Izoh","JamiLitr","Savdo","Plastik","DepozitFarqi","NasiyaJami","QaytganNasiya","XarajatJami","Kutilgan","Farq") VALUES
              (1,1,'2026-10-02 03:00:00',NULL,100000,7830000,1500000,NULL,NULL,NULL,NULL,'0',0,0,0,0,0,0,0,0);
            INSERT INTO "Nasiyalar" ("Id","SmenaId","OperatorId","KimYozdi","MijozIsmi","Telefon","MashinaRaqami","Summa","Qaytgan","Muddat","Yozildi","Yopildi","Izoh") VALUES
              (1,1,1,'Op','Bobur Aliyev','+998 97 700 80 90','01 H 202 MA',600000,200000,'2026-10-20','2026-10-02 04:00:00',NULL,'faol'),
              (2,1,1,'Op','Davron Rahimov','+998 90 000 11 22','',90000,90000,'2026-10-12','2026-10-02 05:00:00','2026-10-02 06:00:00',NULL);
            INSERT INTO "NasiyaQaytishlari" ("Id","NasiyaId","SmenaId","Summa","Usul","Vaqt","OperatorId","KimYozdi","Izoh") VALUES
              (1,1,1,200000,'Naqd','2026-10-02 04:30:00',1,'Op',NULL),
              (2,2,1,90000,'Plastik','2026-10-02 06:00:00',1,'Op',NULL);
            """);
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task Oldingi012Bazasi_YangiUstunNolDefault_NasiyalarOddiyBolibQoladi_SmenaYigindisiOzgarmaydi()
    {
        await using var db = await Otkaz();

        Assert.Contains("20261011065321_NasiyaEskiQarz", await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal("1", await Skalyar("SELECT COUNT(*) FROM pragma_table_info('Nasiyalar') WHERE name = 'EskiQarz' AND \"notnull\" = 1 AND dflt_value = '0'"));
        var nasiyalar = await db.Nasiyalar.AsNoTracking().OrderBy(n => n.Id).ToListAsync();
        Assert.Equal(2, nasiyalar.Count);
        Assert.All(nasiyalar, n => Assert.False(n.EskiQarz));                                                           // eski yozuvlar oddiy nasiya
        Assert.Equal((600_000L, 200_000L, "+998 97 700 80 90", "faol"), (nasiyalar[0].Summa, nasiyalar[0].Qaytgan, nasiyalar[0].Telefon, nasiyalar[0].Izoh));
        Assert.Equal(2, await db.NasiyaQaytishlari.CountAsync());
        Assert.All(nasiyalar, n => Assert.False(n.Dto(new DateOnly(2026, 10, 5)).EskiQarz));

        var yig = await db.Yigindilar(1);                                                                               // smena hisobi: ikkalasi ham NasiyaJami da
        Assert.Equal((690_000L, 290_000L), (yig.NasiyaJami, yig.QaytganNasiya));
        var xulosa = NasiyaXizmati.Xulosa(nasiyalar, await db.NasiyaQaytishlari.AsNoTracking().ToListAsync(), new DateOnly(2026, 10, 5),
            new DateTime(2026, 9, 30, 19, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 19, 0, 0, DateTimeKind.Utc));
        Assert.Equal((690_000L, 2), (xulosa.OyBerilgan, xulosa.OyBerilganSoni));
    }

    [Fact]
    public async Task MigratsiyadanKeyin_EskiQarzYoziladiVaQaytaOqiladi_BazadaBirDeb()
    {
        await using var db = await Otkaz();
        var (nasiya, qaytish) = NasiyaXizmati.EskiQarz(1, 1, "Op", "Hamid", null, null, 120_000, FuelControl.Contracts.TolovTuri.Plastik, null, new DateTime(2026, 10, 2, 7, 0, 0, DateTimeKind.Utc));
        db.Nasiyalar.Add(nasiya);
        await db.SaveChangesAsync();
        qaytish.NasiyaId = nasiya.Id;
        db.NasiyaQaytishlari.Add(qaytish);
        await db.SaveChangesAsync();

        Assert.Equal("1", await Skalyar($"SELECT \"EskiQarz\" FROM \"Nasiyalar\" WHERE \"Id\" = {nasiya.Id}"));
        Assert.Equal("0", await Skalyar("SELECT SUM(\"EskiQarz\") FROM \"Nasiyalar\" WHERE \"Id\" IN (1, 2)"));
        var yig = await db.Yigindilar(1);
        Assert.Equal((690_000L, 410_000L), (yig.NasiyaJami, yig.QaytganNasiya));                                       // eski qarz NasiyaJami ga kirmaydi, qaytishi kiradi
        await using var yangi = Yangi();
        Assert.True((await yangi.Nasiyalar.AsNoTracking().SingleAsync(n => n.Id == nasiya.Id)).EskiQarz);
    }
}
