using System.Globalization;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>
/// Ochiq smenada narx o'zgarishi ko'rsatkichini tuzatish (docs 8.14): aparatning OXIRGI narx o'zgarishi segmenti. Asosiy namuna - 2026-10-17
/// holati: DIZEL ECO narxi o'zgarganda 4-aparatga 109 797.58 o'rniga 1 099 797.58 yozilgan (990 000 L x 15 500).
/// </summary>
public class NarxKorsatkichiTuzatishTests
{
    private const decimal Boshi = 109_797.58m, Xato = 1_099_797.58m;
    private const long EskiNarx = 15_500, YangiNarx = 16_000;
    private static readonly SmenaHisoblagich.Yigindilar Bosh = SmenaHisoblagich.Yigindilar.Bosh;

    private static Dictionary<int, decimal> K(params (int Id, decimal Q)[] v) => v.ToDictionary(x => x.Id, x => x.Q);
    private static decimal D(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);
    private static Smena Ochiq() => new() { Id = 4, OperatorId = 2, Boshlandi = Namuna.Vaqt(3) };
    private static Aparat Dizel() => new() { Id = 4, Raqam = 4, YoqilgiTuriId = 3, TotalLitr = Boshi, BakQoldiq = 5_017.83m };

    private static SmenaKorsatkichi Segment(int tartib, decimal boshi, decimal oxiri, long narx, int smenaId = 4, int aparatId = 4)
    {
        var litr = SmenaHisoblagich.Litr(boshi, oxiri);
        return new SmenaKorsatkichi
        {
            Id = 10 + tartib, SmenaId = smenaId, AparatId = aparatId, Tartib = tartib, Boshi = boshi, Oxiri = oxiri, Narx = narx,
            Litr = litr, Summa = SmenaHisoblagich.Summa(litr, narx), NarxOzgarishida = true, Vaqt = Namuna.Vaqt(10),
        };
    }

    /// <summary>Xato yozilgan segment: 109 797.58 dan 1 099 797.58 gacha, 990 000 L x 15 500.</summary>
    private static SmenaKorsatkichi XatoSegment() => Segment(1, Boshi, Xato, EskiNarx);

    [Fact]
    public void XatoSegment_990000Litr()
    {
        var s = XatoSegment();
        Assert.Equal((990_000.00m, 15_345_000_000L), (s.Litr, s.Summa));
    }

    [Fact]
    public void Tuzatish_OxiriLitrVaSummaQaytaHisoblanadi_NarxPultVaBakOzgarmaydi()
    {
        var smena = Ochiq();
        var aparat = Dizel();
        var segment = XatoSegment();

        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(smena, aparat, [segment], 109_897.58m, "Xato yozilgan");

        Assert.False(n.Ochirildi);
        Assert.Same(segment, n.Segment);
        Assert.Equal((Xato, 109_897.58m), (n.EskiOxiri, n.YangiOxiri));
        // Narx o'zgarguncha 100 L sotilgan: 100 x 15 500 (eski narx o'zgarmaydi).
        Assert.Equal((1, Boshi, 109_897.58m, EskiNarx, 100.00m, 1_550_000L, true),
            (segment.Tartib, segment.Boshi, segment.Oxiri, segment.Narx, segment.Litr, segment.Summa, segment.NarxOzgarishida));
        Assert.Equal((Boshi, 5_017.83m), (aparat.TotalLitr, aparat.BakQoldiq));       // pult va bak faqat yopilganda o'zgaradi
        Assert.True(smena.Ochiqmi);
        Assert.Equal((0L, 0L, 0L), (smena.Savdo, smena.Kutilgan, smena.Farq));         // ochiq smenada pul hali hisoblanmagan
    }

    [Fact]
    public void Tuzatish_QiymatniOshirishHamMumkin()
    {
        var segment = Segment(1, 1000m, 1010m, 12_200);

        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), new Aparat { Id = 4, Raqam = 4, TotalLitr = 1000m }, [segment], 1100.5m, "Sabab");

        Assert.False(n.Ochirildi);
        Assert.Equal((1100.5m, 100.5m, 1_226_100L), (segment.Oxiri, segment.Litr, segment.Summa));      // 100.5 x 12 200
    }

    [Theory]
    [InlineData("109897.575", "109897.58", "100.00")]      // 2 xonaga AwayFromZero: 109 897.575 -> 109 897.58
    [InlineData("109897.574", "109897.57", "99.99")]
    public void Qiymat_YaxlitlanibTuzatiladi(string kiritilgan, string oxiri, string litr)
    {
        var segment = XatoSegment();

        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), Dizel(), [segment], D(kiritilgan), "Sabab");

        Assert.Equal(D(oxiri), n.YangiOxiri);
        Assert.Equal((D(oxiri), D(litr)), (segment.Oxiri, segment.Litr));
    }

    [Fact]
    public void QiymatBoshiga_Teng_SegmentOchiriladi_BoshqaNarsaOzgarmaydi()
    {
        var smena = Ochiq();
        var aparat = Dizel();
        var segment = XatoSegment();

        // Narx o'zgarguncha savdo bo'lmagan: pult 109 797.58 ligicha. 0 litrli segment saqlanmaydi.
        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(smena, aparat, [segment], Boshi, "Pult 10 marta ko'p yozilgan");

        Assert.True(n.Ochirildi);
        Assert.Same(segment, n.Segment);
        Assert.Equal((Xato, Boshi), (n.EskiOxiri, n.YangiOxiri));
        Assert.Equal((Xato, 990_000.00m), (segment.Oxiri, segment.Litr));              // o'chiriladigan qator o'zgartirilmaydi
        Assert.Equal((Boshi, 5_017.83m), (aparat.TotalLitr, aparat.BakQoldiq));
    }

    [Fact]
    public void QiymatBoshiga_YaxlitlanibTeng_SegmentOchiriladi()
    {
        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), Dizel(), [XatoSegment()], 109_797.575m, "Sabab");     // 2 xonaga yaxlitlanadi

        Assert.True(n.Ochirildi);
        Assert.Equal(Boshi, n.YangiOxiri);
    }

    [Theory]
    [InlineData("109797.57")]      // Boshi dan 0.01 kichik
    [InlineData("109797.574")]     // yaxlitlanganda ham kichik
    [InlineData("0")]
    [InlineData("-1")]
    public void QiymatBoshidanKichik_400_KichikKorsatkichMatni(string qiymat)
    {
        var segment = XatoSegment();

        var xato = Assert.Throws<ArgumentException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), Dizel(), [segment], D(qiymat), "Sabab"));

        Assert.Equal("4-aparat: yangi pult ko'rsatkichi oldingi ko'rsatkich (109 797.58) dan kichik bo'lishi mumkin emas.", xato.Message);
        Assert.Equal((Boshi, Xato, 990_000.00m), (segment.Boshi, segment.Oxiri, segment.Litr));      // hech narsa o'zgarmadi
    }

    [Theory]
    [InlineData("1099797.58")]
    [InlineData("1099797.581")]     // yaxlitlanganda joriy ko'rsatkichga teng
    public void QiymatJoriyKorsatkichgaTeng_400(string qiymat)
    {
        var segment = XatoSegment();

        var xato = Assert.Throws<ArgumentException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), Dizel(), [segment], D(qiymat), "Sabab"));

        Assert.Equal("Yangi ko'rsatkich joriy ko'rsatkichdan farq qilishi kerak.", xato.Message);
        Assert.Equal((Xato, 990_000.00m), (segment.Oxiri, segment.Litr));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void SababBosh_400(string sabab)
    {
        var segment = XatoSegment();

        var xato = Assert.Throws<ArgumentException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), Dizel(), [segment], Boshi, sabab));

        Assert.Equal("Tuzatish sababi majburiy.", xato.Message);
        Assert.Equal(Xato, segment.Oxiri);
    }

    [Fact]
    public void SegmentYoq_409_BoshqaAparatBoshqaSmenaYokiTanlanmagan()
    {
        var aparat = Dizel();
        var talab = "Bu aparatda ochiq smenada narx o'zgarishi ko'rsatkichi yo'q.";

        var bosh = Assert.Throws<InvalidOperationException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), aparat, [], Boshi, "Sabab"));
        Assert.Equal(talab, bosh.Message);

        // Boshqa aparatning va boshqa smenaning segmentlari hisobga olinmaydi.
        var boshqa = new[] { Segment(1, 100m, 200m, 12_200, aparatId: 1), Segment(1, Boshi, Xato, EskiNarx, smenaId: 3) };
        Assert.Throws<InvalidOperationException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), aparat, boshqa, Boshi, "Sabab"));
        Assert.Equal((200m, Xato), (boshqa[0].Oxiri, boshqa[1].Oxiri));

        // Narx o'zgarishida qayd etilmagan (yopishda hisoblanadigan) segment tuzatilmaydi.
        var oddiy = Segment(1, Boshi, Xato, EskiNarx);
        oddiy.NarxOzgarishida = false;
        Assert.Throws<InvalidOperationException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), aparat, [oddiy], Boshi, "Sabab"));
    }

    [Fact]
    public void YopilganSmena_409()
    {
        var smena = Ochiq();
        smena.Tugadi = Namuna.Vaqt(20);
        var segment = XatoSegment();

        var xato = Assert.Throws<InvalidOperationException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(smena, Dizel(), [segment], Boshi, "Sabab"));

        Assert.Contains("faqat ochiq smenada", xato.Message);
        Assert.Equal(Xato, segment.Oxiri);
    }

    [Fact]
    public void IkkiSegment_FaqatOxirgisiTuzatiladi_OldingisiTegilmaydi()
    {
        var birinchi = Segment(1, 1000m, 1100m, 12_200);          // birinchi narx o'zgarishi
        var ikkinchi = Segment(2, 1100m, 1500m, 13_000);          // ikkinchisi: 1500 xato, aslida 1150 bo'lishi kerak edi
        var aparat = new Aparat { Id = 4, Raqam = 4, YoqilgiTuriId = 3, TotalLitr = 1000m, BakQoldiq = 900m };

        // Ro'yxat tartibi ahamiyatsiz: oxirgisi - eng katta Tartib.
        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), aparat, [ikkinchi, birinchi], 1150m, "Sabab");

        Assert.Same(ikkinchi, n.Segment);
        Assert.Equal((1100m, 1150m, 50m, 650_000L, 13_000L), (ikkinchi.Boshi, ikkinchi.Oxiri, ikkinchi.Litr, ikkinchi.Summa, ikkinchi.Narx));
        Assert.Equal((1000m, 1100m, 100m, 1_220_000L), (birinchi.Boshi, birinchi.Oxiri, birinchi.Litr, birinchi.Summa));

        // Oxirgi segmentning boshidan kichik qiymat - rad (oldingi segmentga tegib bo'lmaydi), ikkinchisi 1150 da qoladi.
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), aparat, [birinchi, ikkinchi], 1050m, "Sabab"));
        Assert.Equal((1000m, 1100m, 1150m), (birinchi.Boshi, birinchi.Oxiri, ikkinchi.Oxiri));
    }

    [Fact]
    public void IkkiSegment_OxirgisiBoshigaTengBolsa_OchiriladiVaOldingiOxirgiBoladi()
    {
        var birinchi = Segment(1, 1000m, 1100m, 12_200);
        var ikkinchi = Segment(2, 1100m, 1500m, 13_000);
        var aparat = new Aparat { Id = 4, Raqam = 4, YoqilgiTuriId = 3, TotalLitr = 1000m };

        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(Ochiq(), aparat, [birinchi, ikkinchi], 1100m, "Sabab");

        Assert.True(n.Ochirildi);
        Assert.Same(ikkinchi, n.Segment);
        // Segment bazadan o'chirilgach aparatning joriy pult ko'rsatkichi (Oldingi) yana birinchi segmentning oxiri.
        Assert.Equal(1100m, SmenaHisoblagich.Oldingi(aparat, [birinchi]));
    }

    [Fact]
    public void Ssenariy_2026_10_17_TuzatilgachSmenaYopiladi_ToliqYangiNarxdaVaBakdanToGriAyriladi()
    {
        var smena = Ochiq();
        var aparat = Dizel();
        var segment = XatoSegment();
        var narxlar = new Dictionary<int, long> { [3] = YangiNarx };

        // Tuzatishdan oldin smenani yopib bo'lmaydi: yopish ko'rsatkichi 1 099 797.58 dan kichik bo'lolmaydi.
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Yop(smena, [aparat], narxlar, [segment], K((4, 109_897.58m)), 0, 0, 0, null, Bosh, Namuna.Vaqt()));
        Assert.True(smena.Ochiqmi);

        var n = SmenaHisoblagich.NarxKorsatkichiniTuzat(smena, aparat, [segment], Boshi, "Pult 10 marta ko'p yozilgan");
        Assert.True(n.Ochirildi);

        // Segment endi bazada yo'q; 4-aparat yopishda 109 897.58 gacha (100 L) - hammasi yangi narxda (16 000).
        var yopish = SmenaHisoblagich.Yop(smena, [aparat], narxlar, [], K((4, 109_897.58m)), 0, 0, 1_600_000, null, Bosh, Namuna.Vaqt());

        var s = Assert.Single(yopish.YangiSegmentlar);
        Assert.Equal((1, Boshi, 109_897.58m, YangiNarx, 100.00m, 1_600_000L, false), (s.Tartib, s.Boshi, s.Oxiri, s.Narx, s.Litr, s.Summa, s.NarxOzgarishida));
        Assert.Equal((1_600_000L, 100.00m, 0L), (yopish.Natija.Savdo, yopish.Natija.JamiLitr, yopish.Natija.Farq));
        Assert.Equal((109_897.58m, 5_017.83m - 100m), (aparat.TotalLitr, aparat.BakQoldiq));
    }

    [Fact]
    public void Ssenariy_QisqartirilganSegmentBilanYopish_IkkiNarxdaHisoblanadi()
    {
        var smena = Ochiq();
        var aparat = Dizel();
        var segment = XatoSegment();
        var narxlar = new Dictionary<int, long> { [3] = YangiNarx };

        // Narx o'zgarguncha 100 L sotilgan edi (pult 109 897.58): segment qisqaradi, o'chirilmaydi.
        SmenaHisoblagich.NarxKorsatkichiniTuzat(smena, aparat, [segment], 109_897.58m, "Sabab");
        var yopish = SmenaHisoblagich.Yop(smena, [aparat], narxlar, [segment], K((4, 109_997.58m)), 0, 0, 3_150_000, null, Bosh, Namuna.Vaqt());

        Assert.Equal(3_150_000L, yopish.Natija.Savdo);                              // 100 x 15 500 + 100 x 16 000
        Assert.Equal(0L, yopish.Natija.Farq);
        Assert.Equal((109_997.58m, 5_017.83m - 200m), (aparat.TotalLitr, aparat.BakQoldiq));
        Assert.Equal(2, yopish.YangiSegmentlar.Single().Tartib);                     // yopishdagi segment narx o'zgarishidan keyin
    }
}
