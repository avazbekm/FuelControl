using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Core.Xizmatlar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>O'rnatuvchi 0.0.1 bazasi (sotuv modeli) smena hisobi sxemasiga to'g'ri o'tadi.</summary>
public sealed class SmenaHisobiMigratsiyaTests : IDisposable
{
    private const string OxirgiEski = "20261002054024_MaoshOyiYagona";
    private readonly SqliteConnection _ulanish = new("Data Source=:memory:");

    public SmenaHisobiMigratsiyaTests() => _ulanish.Open();

    public void Dispose() => _ulanish.Dispose();

    private FuelControlDbContext Yangi() => new(new DbContextOptionsBuilder<FuelControlDbContext>().UseSqlite(_ulanish).Options);

    private const string EskiMalumot = """
        INSERT INTO "Foydalanuvchilar" ("Id","ToliqIsm","Login","Rol","Faol","OylikMaosh","ParolXeshi","Ruxsatlar","XatoUrinishlar") VALUES
          (1,'Admin','admin','Admin',1,0,'x','Boshqaruv,SotuvKiritish,SmenaOchish,SmenaYopish,Smenalar,Hisobotlar,Eksport,Operatorlar,AvansBerish,SotuvBekorQilish,SotuvTahrirlash,Audit,Sozlamalar',0),
          (2,'Boshliq','bosh','Boshliq',1,0,'x','Boshqaruv,SotuvKiritish,Smenalar,Hisobotlar,Operatorlar,Audit,SmenaOchish,SmenaYopish,SotuvTahrirlash,SotuvBekorQilish,AvansBerish,Eksport',0),
          (3,'Op1','op1','Operator',1,4000000,'x','SotuvKiritish,SmenaOchish,SmenaYopish',0),
          (4,'Op2','op2','Operator',1,4000000,'x','',0),
          (5,'Op3','op3','Operator',1,4000000,'x','SmenaYopish,SotuvKiritish',0);
        INSERT INTO "Yoqilgilar" ("Id","Nomi","Narx","Rang") VALUES (1,'AI-92',12200,'#2563EB');
        INSERT INTO "Aparatlar" ("Id","Raqam","YoqilgiTuriId","TotalLitr") VALUES (1,1,1,'500.50');
        INSERT INTO "Smenalar" ("Id","OperatorId","Boshlandi","Tugadi","KutilganNaqd","KutilganPlastik","KutilganClick","JamiLitr","SotuvSoni","TopshirilganNaqd","TopshirilganPlastik","TopshirilganClick","Izoh") VALUES
          (1,3,'2026-10-01 03:00:00','2026-10-02 03:00:00',100000,50000,20000,'14.00',3,95000,50000,20000,NULL),
          (2,3,'2026-10-02 03:00:00','2026-10-03 03:00:00',200000,80000,0,'20.00',2,210000,80000,0,'izoh'),
          (3,3,'2026-10-03 03:00:00',NULL,0,0,12200,'1.00',1,NULL,NULL,NULL,NULL),
          (4,4,'2026-10-03 04:00:00',NULL,0,0,0,'0',0,NULL,NULL,NULL,NULL);
        INSERT INTO "Sotuvlar" ("Id","SmenaId","OperatorId","AparatId","Narx","Litr","Summa","Vaqt","Holati","IdempotencyKey") VALUES
          (1,1,3,1,12200,'5.00',61000,'2026-10-01 05:00:00','Faol','00000000-0000-0000-0000-000000000001'),
          (2,3,3,1,12200,'1.00',12200,'2026-10-03 06:00:00','Faol','00000000-0000-0000-0000-000000000002');
        INSERT INTO "SotuvTolovlari" ("Id","SotuvId","Turi","Summa") VALUES (1,1,'Naqd',61000),(2,2,'Click',12200);
        INSERT INTO "Audit" ("Id","Vaqt","Kim","Amal","Tafsilot") VALUES
          (1,'2026-10-01 03:00:00','Op1','Smena ochildi',''), (2,'2026-10-02 03:00:00','Op1','Smena yopildi',''),
          (3,'2026-10-02 04:00:00','Admin','Sotuv tahrirlandi',''), (4,'2026-10-02 05:00:00','Admin','Narx o''zgartirildi',''),
          (5,'2026-10-02 06:00:00','Admin','Avans berildi',''), (6,'2026-10-02 07:00:00','Admin','Eksport: Hisob-varaqa',''),
          (7,'2026-10-02 08:00:00','Admin','Eksport: Hisobot',''), (8,'2026-10-02 09:00:00','Admin','Foydalanuvchi yaratildi','');
        INSERT INTO "Harakatlar" ("Id","OperatorId","Sana","Turi","Summa","Izoh","KimYozdi") VALUES (1,3,'2026-10-02 03:00:00','Kamomat',-5000,'Smena #1 kamomati','Tizim');
        """;

    /// <summary>0.0.1 sxemasidagi baza (eski sotuv modeli, ikkita operatorning ochiq smenasi bilan), so'ng oxirgi migratsiyagacha.</summary>
    private async Task<FuelControlDbContext> Otkaz()
    {
        var db = Yangi();
        await db.GetService<IMigrator>().MigrateAsync(OxirgiEski);
        await db.Database.ExecuteSqlRawAsync(EskiMalumot);
        await db.Database.MigrateAsync();
        return db;
    }

    private async Task<string> Skalyar(string sql)
    {
        await using var buyruq = _ulanish.CreateCommand();
        buyruq.CommandText = sql;
        return Convert.ToString(await buyruq.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    [Fact]
    public async Task EskiSmenalarTarixSifatidaOtadi_OchiqlariYopiladi_FarqOperatorHisobiBilanMos()
    {
        await using var db = await Otkaz();
        var s = await db.Smenalar.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

        Assert.Equal(4, s.Count);
        Assert.All(s, x => Assert.False(x.Ochiqmi));

        // #1: kutilgan 100 000 naqd + 50 000 plastik + 20 000 click; topshirilgan 95 000 / 50 000 / 20 000 -> kamomat 5 000.
        Assert.Equal((170_000L, 50_000L, 20_000L, 0L, 14.00m), (s[0].Savdo, s[0].Plastik, s[0].DepozitFarqi, s[0].NasiyaJami, s[0].JamiLitr));
        Assert.Equal((0L, 0L, 0L, 50_000L, 20_000L), (s[0].OchishQaytim, s[0].OchishTerminal, s[0].OchishDepozit, s[0].YopishTerminal ?? -1, s[0].YopishDepozit ?? -1));
        Assert.Equal((95_000L, 100_000L, -5_000L), (s[0].SanalganNaqd, s[0].Kutilgan, s[0].Farq));
        Assert.Equal(-5_000, (await db.Harakatlar.SingleAsync()).Summa);      // operator hisobidagi harakat o'zgarmagan

        // #2: ortiqcha 10 000, izoh saqlangan.
        Assert.Equal((280_000L, 10_000L, 200_000L, "izoh"), (s[1].Savdo, s[1].Farq, s[1].Kutilgan, s[1].Izoh));

        // #3 va #4: ochiq edi - oxirgi sotuv vaqtida (yo'q bo'lsa boshlanishida) yopildi, soxta kamomat yo'q.
        Assert.Equal(new DateTime(2026, 10, 3, 6, 0, 0, DateTimeKind.Utc), s[2].Tugadi);
        Assert.Equal(s[3].Boshlandi, s[3].Tugadi);
        Assert.Equal((12_200L, 0L, 0L), (s[2].Savdo, s[2].Farq, s[2].Kutilgan));
        Assert.Equal((0L, 0L), (s[3].Savdo, s[3].Farq));
    }

    [Fact]
    public async Task SotuvJadvallariOchdi_AparatBakQoldigiNol_AuditTurlariYozildi()
    {
        await using var db = await Otkaz();
        Assert.Equal("0", await Skalyar("SELECT COUNT(*) FROM sqlite_master WHERE name IN ('Sotuvlar','SotuvTolovlari')"));
        Assert.Equal("1", await Skalyar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'SmenaKorsatkichlari'"));

        var aparat = await db.Aparatlar.AsNoTracking().SingleAsync();
        Assert.Equal((500.50m, 0m), (aparat.TotalLitr, aparat.BakQoldiq));

        var turlar = await db.Audit.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Tur).ToListAsync();
        Assert.Equal(["smena", "smena", "tuzatish", "sozlama", "hisob", "hisob", "sozlama", "sozlama"], turlar);
    }

    [Fact]
    public async Task RuxsatlarMoslashadi_RolBoyichaYangiStandartlar_TakrorsizVaVergulsizChetlar()
    {
        await using var db = await Otkaz();
        var f = await db.Foydalanuvchilar.AsNoTracking().OrderBy(x => x.Id).ToListAsync();

        Assert.Equal(RuxsatXizmati.Hammasi.ToHashSet(), f[0].Ruxsatlar.ToHashSet());                 // admin: hammasi
        Assert.Equal(RuxsatXizmati.Standart(Rol.Boshliq), f[1].Ruxsatlar.ToHashSet());               // boshliq: Sozlamalar'dan tashqari
        Assert.Equal(RuxsatXizmati.Standart(Rol.Operator), f[2].Ruxsatlar.ToHashSet());
        Assert.Equal(new HashSet<Ruxsat> { Ruxsat.Nasiyalar, Ruxsat.NasiyaYozish, Ruxsat.QarzQaytdi, Ruxsat.XarajatYozish }, f[3].Ruxsatlar.ToHashSet());   // bo'sh edi
        Assert.Equal(new HashSet<Ruxsat> { Ruxsat.SmenaYopish, Ruxsat.Savdo, Ruxsat.Nasiyalar, Ruxsat.NasiyaYozish, Ruxsat.QarzQaytdi, Ruxsat.XarajatYozish }, f[4].Ruxsatlar.ToHashSet());
        Assert.All(f, x => Assert.Equal(x.Ruxsatlar.Count, x.Ruxsatlar.Distinct().Count()));

        // Bazadagi matn toza: eski nomlar, bo'sh elementlar va chetdagi vergul yo'q.
        var matnlar = (await Skalyar("SELECT group_concat(\"Ruxsatlar\", '|') FROM \"Foydalanuvchilar\"")).Split('|');
        Assert.All(matnlar, m =>
        {
            Assert.DoesNotContain("Sotuv", m);
            Assert.DoesNotContain(",,", m);
            Assert.False(m.StartsWith(',') || m.EndsWith(','));
        });
    }

    [Fact]
    public async Task BittaOchiqSmenaIndeksi_IkkinchiOchiqSmenaniBazaRadEtadi()
    {
        await using var db = await Otkaz();
        Assert.Equal("1", await Skalyar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'IX_Smenalar_BittaOchiq'"));
        Assert.Equal("0", await Skalyar("SELECT COUNT(*) FROM \"Smenalar\" WHERE \"Tugadi\" IS NULL"));

        var birinchi = SmenaHisoblagich.Och(3, 0, 0, 0, DateTime.UtcNow);
        db.Smenalar.Add(birinchi);
        await db.SaveChangesAsync();

        await using var raqib = Yangi();
        raqib.Smenalar.Add(SmenaHisoblagich.Och(4, 0, 0, 0, DateTime.UtcNow));
        var xato = await Assert.ThrowsAsync<DbUpdateException>(() => raqib.SaveChangesAsync());
        Assert.Equal(19, Assert.IsType<SqliteException>(xato.InnerException).SqliteErrorCode);

        // Yopilgach yangisini ochish mumkin; yopilganlar soni cheklanmaydi.
        SmenaHisoblagich.Yop(birinchi, [], new Dictionary<int, long>(), [], new Dictionary<int, decimal>(), 0, 0, 0, null, SmenaHisoblagich.Yigindilar.Bosh, DateTime.UtcNow);
        await db.SaveChangesAsync();
        raqib.ChangeTracker.Clear();
        raqib.Smenalar.Add(SmenaHisoblagich.Och(4, 0, 0, 0, DateTime.UtcNow));
        await raqib.SaveChangesAsync();
    }

    [Fact]
    public async Task TozaBaza_Migratsiyalardan_ModelBilanMos()
    {
        // Bo'sh bazadan oxirgi migratsiyagacha: EF modeli migratsiyalar snapshot'i bilan mos (kutilayotgan o'zgarish yo'q), indeks bor.
        await using var db = Yangi();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("1", await Skalyar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'IX_Smenalar_BittaOchiq'"));
    }
}
