using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using FuelControl.Api.Xizmatlar;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FuelControl.Api.Tests;

public sealed class MaoshTests : SqliteBaza
{
    private static readonly DateTime Hozir = new(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);

    /// <summary>Boshqa API nusxasi shu oy maoshini kontekst saqlashidan oldinroq yozib qo'yadi.</summary>
    private sealed class RaqibMaoshYozadi(Func<FuelControlDbContext> yangi, int operatorId) : SaveChangesInterceptor
    {
        public bool Yozdi { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData e, InterceptionResult<int> natija, CancellationToken ct = default)
        {
            if (!Yozdi && e.Context!.ChangeTracker.Entries<HisobHarakati>().Any(x => x.State == EntityState.Added))
            {
                Yozdi = true;
                await using var raqib = yangi();
                raqib.Harakatlar.Add(new HisobHarakati
                {
                    OperatorId = operatorId, Sana = Hozir, Turi = HarakatTuri.Maosh, Summa = 1, MaoshOyi = "2026-10", KimYozdi = "Raqib",
                });
                await raqib.SaveChangesAsync(ct);
            }
            return natija;
        }
    }

    private async Task MaoshQoy(long summa)
    {
        await using var db = Yangi();
        var op = await db.Foydalanuvchilar.SingleAsync();
        op.OylikMaosh = summa;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task IkkiMartaChaqirilsa_BittaYoziladi()
    {
        await MaoshQoy(4_000_000);
        await using (var a = Yangi()) await MaoshYozuvchi.Yoz(a, Hozir);
        await using (var b = Yangi()) await MaoshYozuvchi.Yoz(b, Hozir);

        await using var t = Yangi();
        var m = await t.Harakatlar.SingleAsync();
        Assert.Equal("2026-10", m.MaoshOyi);
        Assert.Equal(4_000_000, m.Summa);
    }

    [Fact]
    public async Task ParallelNusxaOldinYozsa_JimOtadi_Dublikat_Yoq()
    {
        await MaoshQoy(4_000_000);
        var raqib = new RaqibMaoshYozadi(() => Yangi(), OperatorId);
        await using (var db = Yangi(raqib)) await MaoshYozuvchi.Yoz(db, Hozir); // istisno chiqmasligi kerak

        Assert.True(raqib.Yozdi);
        await using var t = Yangi();
        var m = await t.Harakatlar.SingleAsync();
        Assert.Equal("Raqib", m.KimYozdi);
    }

    [Fact]
    public async Task KeyingiOyUchunYangiYozuv()
    {
        await MaoshQoy(4_000_000);
        await using (var a = Yangi()) await MaoshYozuvchi.Yoz(a, Hozir);
        await using (var b = Yangi()) await MaoshYozuvchi.Yoz(b, Hozir.AddMonths(1));
        await using var t = Yangi();
        Assert.Equal(["2026-10", "2026-11"], await t.Harakatlar.OrderBy(h => h.Id).Select(h => h.MaoshOyi).ToListAsync());
    }
}

/// <summary>MaoshOyiYagona migratsiyasi eski bazadagi ikki marta yozilgan maoshlarni tozalaydi.</summary>
public sealed class MaoshMigratsiyaTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _ulanish = new("Data Source=:memory:");

    public MaoshMigratsiyaTests() => _ulanish.Open();

    [Fact]
    public async Task DublikatlarTozalanadi_EngKichikIdQoladi()
    {
        await using var db = new FuelControlDbContext(new DbContextOptionsBuilder<FuelControlDbContext>().UseSqlite(_ulanish).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261002045902_OchiqSmenaYagona");

        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "Foydalanuvchilar" ("Id","ToliqIsm","Login","Rol","Faol","OylikMaosh","ParolXeshi","Ruxsatlar","XatoUrinishlar")
            VALUES (2,'A','a','Operator',1,100,'x','',0), (3,'B','b','Operator',1,100,'x','',0);
            INSERT INTO "Harakatlar" ("Id","OperatorId","Sana","Turi","Summa","Izoh","KimYozdi") VALUES
              (1,2,'2026-10-01 00:00:00','Maosh',100,'',''), (2,2,'2026-10-01 00:00:00','Maosh',100,'',''),
              (3,3,'2026-10-01 00:00:00','Maosh',100,'',''), (4,3,'2026-10-01 00:00:00','Maosh',100,'',''),
              (5,2,'2026-09-01 00:00:00','Maosh',100,'',''), (6,2,'2026-10-05 00:00:00','Avans',-50,'','');
            """);

        await migrator.MigrateAsync();

        var qolgan = await db.Harakatlar.OrderBy(h => h.Id).Select(h => new { h.Id, h.MaoshOyi }).ToListAsync();
        Assert.Equal([1, 3, 5, 6], qolgan.Select(x => x.Id));
        Assert.Equal(["2026-10", "2026-10", "2026-09", null], qolgan.Select(x => x.MaoshOyi));
    }

    public void Dispose() => _ulanish.Dispose();
}

public sealed class BittaTolovTuriTests : SqliteBaza
{
    [Fact]
    public async Task TolovTuriBerilsa_SummaniServerQoyadi()
    {
        await using var db = Yangi();
        var (s, _) = await SotuvXizmati.Yarat(db, OperatorId, new SotuvYaratishDto(AparatId, 10m, null, null, Guid.NewGuid(), TolovTuri.Click));
        var t = Assert.Single(s.Tolovlar);
        Assert.Equal(TolovTuri.Click, t.Turi);
        Assert.Equal(122_000, t.Summa);
        Assert.Equal(122_000, s.Summa);
    }

    [Fact]
    public async Task IkkalasiYokiHechBiri_RadEtiladi()
    {
        await using var db = Yangi();
        await Assert.ThrowsAsync<BiznesXatosi>(() => SotuvXizmati.Yarat(db, OperatorId,
            new SotuvYaratishDto(AparatId, null, 100_000, [new TolovDto(TolovTuri.Naqd, 100_000)], Guid.NewGuid(), TolovTuri.Naqd)));
        await Assert.ThrowsAsync<BiznesXatosi>(() => SotuvXizmati.Yarat(db, OperatorId,
            new SotuvYaratishDto(AparatId, null, 100_000, null, Guid.NewGuid())));
        await Assert.ThrowsAsync<BiznesXatosi>(() => SotuvXizmati.Yarat(db, OperatorId,
            new SotuvYaratishDto(AparatId, null, 100_000, [], Guid.NewGuid())));
    }
}
