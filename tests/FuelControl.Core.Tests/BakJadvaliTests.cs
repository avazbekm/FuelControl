using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>Hisobotdagi aparat/bak jadvali (BakBoshida + Kirim - Sotildi = BakOxirida) va Boshqaruv yig'indilari.</summary>
public class BakJadvaliTests
{
    private static DateTime U(int oy, int kun, int soat = 3) => new(2026, oy, kun, soat, 0, 0, DateTimeKind.Utc);
    private static readonly Dictionary<int, string> Yoqilgilar = new() { [1] = "AI-92" };

    // Davr: 1-oktabr 00:00 Toshkent (30-sent 19:00 UTC) .. 4-oktabr 00:00 Toshkent (3-okt 19:00 UTC).
    private static readonly DateTime Dan = U(9, 30, 19), Gacha = U(10, 3, 19);

    private static Smena Yopilgan(int id, DateTime boshlandi) => new() { Id = id, Boshlandi = boshlandi, Tugadi = boshlandi.AddHours(24) };
    private static SmenaKorsatkichi Seg(int smena, decimal litr, long narx = 12_200) =>
        new() { SmenaId = smena, AparatId = 1, Tartib = 1, Litr = litr, Narx = narx, Summa = (long)(litr * narx) };

    [Fact]
    public void DavrIchidagiHarakatlar_BoshidaKirimSotildiOxirida_Mos()
    {
        var aparat = new Aparat { Id = 1, Raqam = 1, YoqilgiTuriId = 1, BakQoldiq = 100m };
        var smenalar = new[] { Yopilgan(1, U(10, 1)), Yopilgan(2, U(10, 2)), Yopilgan(3, U(10, 4)) };   // 3-chisi davrdan keyin ochilgan
        var segmentlar = new[] { Seg(1, 30m), Seg(2, 20m), Seg(3, 7m) };
        var kirimlar = new[]
        {
            new BakKirim { AparatId = 1, Litr = 50m, Vaqt = U(10, 2, 6) },    // davrda
            new BakKirim { AparatId = 1, Litr = 20m, Vaqt = U(10, 5) },       // davrdan keyin
        };
        var tuzatishlar = new[] { new BakTuzatishi { AparatId = 1, Oldin = 90m, Keyin = 95m, Vaqt = U(10, 6) } };   // davrdan keyin +5

        var q = Assert.Single(HisobotXizmati.Aparatlar([aparat], Yoqilgilar, smenalar, segmentlar, kirimlar, tuzatishlar, Dan, Gacha));

        Assert.Equal((1, "AI-92", 50m, 50m, 366_000L + 244_000), (q.Raqam, q.YoqilgiNomi, q.Kirim, q.Sotildi, q.Savdo));
        Assert.Equal(82m, q.BakOxirida);                                      // 100 - 20 (keyingi kirim) + 7 (keyingi sotilgan) - 5 (keyingi tuzatish)
        Assert.Equal(82m, q.BakBoshida);                                      // 82 - 50 + 50
        Assert.Equal(q.BakBoshida + q.Kirim - q.Sotildi, q.BakOxirida);
    }

    [Fact]
    public void DavrOxiriBelgilanmasa_BakOxiridaHozirgiQoldiq()
    {
        var aparat = new Aparat { Id = 1, Raqam = 1, YoqilgiTuriId = 1, BakQoldiq = 100m };
        var smenalar = new[] { Yopilgan(1, U(10, 1)), Yopilgan(2, U(10, 4)) };
        var segmentlar = new[] { Seg(1, 30m), Seg(2, 7m) };
        var kirimlar = new[] { new BakKirim { AparatId = 1, Litr = 50m, Vaqt = U(10, 2, 6) }, new BakKirim { AparatId = 1, Litr = 20m, Vaqt = U(10, 5) } };

        var q = Assert.Single(HisobotXizmati.Aparatlar([aparat], Yoqilgilar, smenalar, segmentlar, kirimlar, [], Dan, null));

        Assert.Equal((100m, 70m, 37m), (q.BakOxirida, q.Kirim, q.Sotildi));
        Assert.Equal(67m, q.BakBoshida);                                      // 100 - 70 + 37
    }

    [Fact]
    public void DavrdanOldingiSmenaVaKirim_HisobgaOlinmaydi()
    {
        var aparat = new Aparat { Id = 1, Raqam = 1, YoqilgiTuriId = 1, BakQoldiq = 500m };
        var smenalar = new[] { Yopilgan(1, U(9, 20)), Yopilgan(2, U(10, 2)) };   // 1-smena davrdan oldin (dan'dan keyingilar yuklanadi, lekin davrga kirmaydi)
        var segmentlar = new[] { Seg(1, 999m), Seg(2, 10m) };
        var kirimlar = new[] { new BakKirim { AparatId = 1, Litr = 777m, Vaqt = U(9, 20) } };

        var q = Assert.Single(HisobotXizmati.Aparatlar([aparat], Yoqilgilar, smenalar.Skip(1).ToArray(), segmentlar, kirimlar, [], Dan, Gacha));

        Assert.Equal((10m, 0m), (q.Sotildi, q.Kirim));
    }

    [Fact]
    public void AparatlarRaqamBoyicha_NomaLumYoqilgiBelgisi()
    {
        var a = new[] { new Aparat { Id = 2, Raqam = 2, YoqilgiTuriId = 9 }, new Aparat { Id = 1, Raqam = 1, YoqilgiTuriId = 1 } };
        var q = HisobotXizmati.Aparatlar(a, Yoqilgilar, [], [], [], [], null, null);
        Assert.Equal([1, 2], q.Select(x => x.Raqam));
        Assert.Equal(["AI-92", "?"], q.Select(x => x.YoqilgiNomi));
    }

    [Fact]
    public void Boshqaruv_OyKorsatkichlari_TolovTaqsimotiVaOxirgiSmenalar()
    {
        var oy = new List<Smena>
        {
            new() { Id = 39, Boshlandi = U(10, 1), Savdo = 100, JamiLitr = 1m, Plastik = 30, DepozitFarqi = 10, NasiyaJami = 5, Farq = 20 },
            new() { Id = 40, Boshlandi = U(10, 2), Savdo = 200, JamiLitr = 2m, Plastik = 60, DepozitFarqi = -10, NasiyaJami = 15, Farq = -120 },
        };

        var k = BoshqaruvXizmati.Oy(oy);

        Assert.Equal((300L, 3m, 2, 120L, 20L), (k.Savdo, k.Litr, k.SmenaSoni, k.Kamomat, k.Ortiqcha));
        Assert.Equal((300L - 90 - 0 - 20, 90L, 0L, 20L), (k.Tolovlar.Naqd, k.Tolovlar.Plastik, k.Tolovlar.Depozit, k.Tolovlar.Nasiya));
    }

    [Fact]
    public void OxirgiSmenalar_YangiOnTortaOlinadi_EskisidanYangisiga()
    {
        var hammasi = Enumerable.Range(1, 20).Select(i => new Smena { Id = i, OperatorId = 2, Boshlandi = U(9, 1).AddDays(i), Savdo = i * 10, JamiLitr = i, Farq = -i }).ToList();
        var q = BoshqaruvXizmati.OxirgiSmenalar(hammasi, new Dictionary<int, string> { [2] = "Alisher Karimov" });

        Assert.Equal(14, q.Length);
        Assert.Equal((7, 20), (q[0].Id, q[^1].Id));
        Assert.Equal(("Alisher Karimov", 200L, 20m, -20L), (q[^1].OperatorIsmi, q[^1].Savdo, q[^1].Litr, q[^1].Farq));
        Assert.Equal(new DateOnly(2026, 9, 21), q[^1].Sana);
    }
}
