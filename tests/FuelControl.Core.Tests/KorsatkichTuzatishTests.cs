using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>Oxirgi yopilgan smenada pult ko'rsatkichini tuzatish (sotuv tahririning o'rni).</summary>
public class KorsatkichTuzatishTests
{
    /// <summary>Dizayndagi namuna smena yopilgan holatda (kamomat 45 520): smena, aparatlar va uning segmentlari.</summary>
    private static (Smena Smena, List<Aparat> Aparatlar, List<SmenaKorsatkichi> Segmentlar) Yopilgan()
    {
        var smena = Namuna.OchiqSmena();
        var aparatlar = Namuna.Aparatlar();
        var yopish = Namuna.Yop(smena, aparatlar);
        return (smena, aparatlar, yopish.YangiSegmentlar.ToList());
    }

    [Fact]
    public void KorsatkichniOshirish_SavdoOshadi_KamomatFarqiHarakatiYoziladi()
    {
        var (smena, aparatlar, segmentlar) = Yopilgan();
        Assert.Equal(-45_520, smena.Farq);

        // 1-aparat: 184 642.30 -> 184 652.30 (+10.00 L x 12 200 = +122 000 so'm)
        var t = SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [], 184_652.30m, "Pult noto'g'ri o'qilgan", "Boshliq", Namuna.Vaqt(15));

        Assert.Equal((-45_520L, -167_520L), (t.Eski.Farq, t.Yangi.Farq));
        Assert.Equal((16_590_520L, 5_045_520L, -167_520L), (smena.Savdo, smena.Kutilgan, smena.Farq));
        Assert.Equal(1246.80m, smena.JamiLitr);
        Assert.Equal((184_642.30m, 184_652.30m), (t.EskiOxiri, t.YangiOxiri));

        // Farq o'zgarishi uchun tuzatuvchi harakat: qo'shimcha kamomat -122 000, "Smena #42 tuzatish".
        var h = Assert.IsType<HisobHarakati>(t.Harakat);
        Assert.Equal((HarakatTuri.Kamomat, -122_000L, 2, "Boshliq"), (h.Turi, h.Summa, h.OperatorId, h.KimYozdi));
        Assert.Equal("Smena #42 tuzatish: 1-aparat 184 642.30 dan 184 652.30 ga. Sabab: Pult noto'g'ri o'qilgan", h.Izoh);

        // Aparat: TotalLitr +10, bak -10 (delta bo'yicha).
        Assert.Equal((184_652.30m, 6_418.20m), (aparatlar[0].TotalLitr, aparatlar[0].BakQoldiq));
        Assert.Equal(5_145_960, segmentlar[0].Summa);
    }

    [Fact]
    public void KorsatkichniKamaytirish_OrtiqchaHarakatiYoziladi()
    {
        var (smena, aparatlar, segmentlar) = Yopilgan();

        // -5.00 L x 12 200 = -61 000 so'm savdo: kutilgan kamayadi, farq +61 000 ga yaxshilanadi.
        var t = SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [], 184_637.30m, "Xato", "Boshliq", Namuna.Vaqt(15));

        Assert.Equal(15_480, smena.Farq);                       // -45 520 + 61 000
        var h = Assert.IsType<HisobHarakati>(t.Harakat);
        Assert.Equal((HarakatTuri.Ortiqcha, 61_000L), (h.Turi, h.Summa));
        Assert.Equal((184_637.30m, 6_433.20m), (aparatlar[0].TotalLitr, aparatlar[0].BakQoldiq));
    }

    [Fact]
    public void QolidaOzgartirilganTotalLitr_DeltaBoyichaTuzatiladi()
    {
        var (smena, aparatlar, segmentlar) = Yopilgan();
        aparatlar[0].TotalLitr += 1000m;                        // pult almashtirilib, Sozlamalarda qo'lda tuzatilgan edi

        SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [], 184_652.30m, "Xato", "Boshliq", Namuna.Vaqt(15));

        Assert.Equal(185_652.30m, aparatlar[0].TotalLitr);      // 184 642.30 + 1000 + 10
    }

    [Fact]
    public void KeyingiOchiqSmenadagiNarxSegmenti_BoshlanishiYangilanadi()
    {
        var (smena, aparatlar, segmentlar) = Yopilgan();
        // Keyingi (ochiq) smenada narx 184 700.00 da o'zgargan: birinchi segment 184 642.30 dan boshlangan edi.
        var keyingi = new SmenaKorsatkichi { SmenaId = 43, AparatId = 1, Tartib = 1, Boshi = 184_642.30m, Oxiri = 184_700.00m, Narx = 12_200, Litr = 57.70m, Summa = 703_940, NarxOzgarishida = true };

        SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [keyingi], 184_652.30m, "Xato", "Boshliq", Namuna.Vaqt(15));

        Assert.Equal((184_652.30m, 184_700.00m, 47.70m, 581_940L), (keyingi.Boshi, keyingi.Oxiri, keyingi.Litr, keyingi.Summa));
    }

    [Fact]
    public void KeyingiSmenaSegmentidanKattaKorsatkich_RadEtiladi_HechNarsaOzgarmaydi()
    {
        var (smena, aparatlar, segmentlar) = Yopilgan();
        var keyingi = new SmenaKorsatkichi { SmenaId = 43, AparatId = 1, Tartib = 1, Boshi = 184_642.30m, Oxiri = 184_700.00m, Narx = 12_200, Litr = 57.70m, Summa = 703_940 };

        Assert.Throws<ArgumentException>(() =>
            SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [keyingi], 184_700.01m, "Xato", "Boshliq", Namuna.Vaqt(15)));

        Assert.Equal((-45_520L, 184_642.30m, 57.70m), (smena.Farq, segmentlar[0].Oxiri, keyingi.Litr));
        Assert.Equal(184_642.30m, aparatlar[0].TotalLitr);
    }

    [Fact]
    public void IkkiSegmentliAparat_OxirgiSegmentTuzatiladi_UningBoshlanishidanKichikRadEtiladi()
    {
        var smena = new Smena { Id = 3, OperatorId = 1, Boshlandi = Namuna.Vaqt(3), OchishQaytim = 0 };
        var aparat = new Aparat { Id = 1, Raqam = 1, YoqilgiTuriId = 1, TotalLitr = 1000m, BakQoldiq = 5000m };
        var bir = SmenaHisoblagich.NarxOzgarishi(smena, 12_200, [aparat], [], new Dictionary<int, decimal> { [1] = 1100m }, Namuna.Vaqt(9));
        var yopish = SmenaHisoblagich.Yop(smena, [aparat], new Dictionary<int, long> { [1] = 13_000 }, bir, new Dictionary<int, decimal> { [1] = 1250m },
            0, 0, 3_000_000, null, SmenaHisoblagich.Yigindilar.Bosh, Namuna.Vaqt());
        var hammasi = bir.Concat(yopish.YangiSegmentlar).ToList();

        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.KorsatkichniTuzat(smena, aparat, hammasi, [], 1099m, "Xato", "Boshliq", Namuna.Vaqt()));

        var t = SmenaHisoblagich.KorsatkichniTuzat(smena, aparat, hammasi, [], 1260m, "Xato", "Boshliq", Namuna.Vaqt());
        Assert.Equal(1260m, hammasi.Single(x => x.Tartib == 2).Oxiri);
        Assert.Equal(1_220_000 + 160 * 13_000, smena.Savdo);   // birinchi segment (eski narx) o'zgarmadi
        Assert.Equal(1260m, aparat.TotalLitr);
        Assert.NotNull(t.Harakat);
    }

    [Fact]
    public void Tuzatish_ShartlarBuzilsaRadEtiladi()
    {
        var (smena, aparatlar, segmentlar) = Yopilgan();
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [], 184_652.30m, "  ", "B", Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [], 184_642.30m, "Xato", "B", Namuna.Vaqt()));   // o'zgarmagan
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], segmentlar, [], 184_230.49m, "Xato", "B", Namuna.Vaqt()));   // boshlanishidan kichik
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.KorsatkichniTuzat(smena, new Aparat { Id = 77, Raqam = 7 }, segmentlar, [], 5m, "Xato", "B", Namuna.Vaqt()));
        Assert.Throws<InvalidOperationException>(() => SmenaHisoblagich.KorsatkichniTuzat(new Smena { Id = 5 }, aparatlar[0], segmentlar, [], 184_652.30m, "Xato", "B", Namuna.Vaqt()));
        Assert.Equal(-45_520, smena.Farq);                      // hech narsa o'zgarmadi
    }
}
