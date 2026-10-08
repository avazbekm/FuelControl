using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>Ochiq smenada narx o'zgarishi: aparatda 2 (yoki ko'proq) segment.</summary>
public class SmenaSegmentTests
{
    private static readonly SmenaHisoblagich.Yigindilar Bosh = SmenaHisoblagich.Yigindilar.Bosh;
    private static Dictionary<int, decimal> K(params (int Id, decimal Q)[] v) => v.ToDictionary(x => x.Id, x => x.Q);
    private static Aparat Bitta(decimal total = 1000.00m, decimal bak = 5000m) =>
        new() { Id = 1, Raqam = 1, YoqilgiTuriId = 1, TotalLitr = total, BakQoldiq = bak };

    [Fact]
    public void NarxOzgarishi_IkkiSegment_EskiNarxdaVaYangiNarxda()
    {
        var smena = new Smena { Id = 3, OperatorId = 1, Boshlandi = Namuna.Vaqt(3) };
        var aparat = Bitta();

        var birinchi = SmenaHisoblagich.NarxOzgarishi(smena, 12_200, [aparat], [], K((1, 1100m)), Namuna.Vaqt(10));

        var s1 = Assert.Single(birinchi);
        Assert.Equal((1, 1000.00m, 1100m, 12_200L, 100.00m, 1_220_000L, true), (s1.Tartib, s1.Boshi, s1.Oxiri, s1.Narx, s1.Litr, s1.Summa, s1.NarxOzgarishida));
        Assert.Equal((1000.00m, 5000m), (aparat.TotalLitr, aparat.BakQoldiq));   // TotalLitr va bak faqat yopilganda o'zgaradi

        var yopish = SmenaHisoblagich.Yop(smena, [aparat], new Dictionary<int, long> { [1] = 13_000 }, birinchi, K((1, 1250.50m)),
            0, 0, 0, null, Bosh, Namuna.Vaqt());

        var s2 = Assert.Single(yopish.YangiSegmentlar);
        Assert.Equal((2, 1100m, 1250.50m, 13_000L, 150.50m, 1_956_500L, false), (s2.Tartib, s2.Boshi, s2.Oxiri, s2.Narx, s2.Litr, s2.Summa, s2.NarxOzgarishida));
        Assert.Equal(3_176_500, yopish.Natija.Savdo);          // 1 220 000 + 1 956 500
        Assert.Equal(250.50m, yopish.Natija.JamiLitr);
        Assert.Equal((1250.50m, 5000m - 250.50m), (aparat.TotalLitr, aparat.BakQoldiq));
    }

    [Fact]
    public void NarxOzgarishi_IkkinchiMarta_SegmentlarKetmaKet()
    {
        var smena = new Smena { Id = 3 };
        var aparat = Bitta();
        var bir = SmenaHisoblagich.NarxOzgarishi(smena, 12_200, [aparat], [], K((1, 1100m)), Namuna.Vaqt(9));
        var ikki = SmenaHisoblagich.NarxOzgarishi(smena, 13_000, [aparat], bir, K((1, 1150m)), Namuna.Vaqt(11));

        var s = Assert.Single(ikki);
        Assert.Equal((2, 1100m, 1150m, 13_000L, 50m, 650_000L, true), (s.Tartib, s.Boshi, s.Oxiri, s.Narx, s.Litr, s.Summa, s.NarxOzgarishida));

        var yopish = SmenaHisoblagich.Yop(smena, [aparat], new Dictionary<int, long> { [1] = 14_000 }, bir.Concat(ikki).ToList(), K((1, 1200m)),
            0, 0, 0, null, Bosh, Namuna.Vaqt());
        Assert.Equal(3, yopish.YangiSegmentlar.Single().Tartib);
        Assert.Equal(1_220_000 + 650_000 + 700_000, yopish.Natija.Savdo);
    }

    [Fact]
    public void NarxOzgarishi_KorsatkichYetishmasa_KerakliAparatlarBilanXato()
    {
        var smena = new Smena { Id = 3 };
        var a1 = new Aparat { Id = 11, Raqam = 1, YoqilgiTuriId = 1, TotalLitr = 100m };
        var a2 = new Aparat { Id = 12, Raqam = 2, YoqilgiTuriId = 1, TotalLitr = 200m };

        var xato = Assert.Throws<KorsatkichKerakXatosi>(() =>
            SmenaHisoblagich.NarxOzgarishi(smena, 12_200, [a1, a2], [], K((11, 150m)), Namuna.Vaqt()));

        Assert.Equal([12], xato.KerakliAparatlar);
        Assert.IsAssignableFrom<ArgumentException>(xato);      // Api'da 400
    }

    [Fact]
    public void NarxOzgarishi_KichikKorsatkichRadEtiladi_OldingisiTotalLitrYokiOxirgiSegment()
    {
        var smena = new Smena { Id = 3 };
        var aparat = Bitta();
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.NarxOzgarishi(smena, 12_200, [aparat], [], K((1, 999.99m)), Namuna.Vaqt()));

        var bir = SmenaHisoblagich.NarxOzgarishi(smena, 12_200, [aparat], [], K((1, 1100m)), Namuna.Vaqt());
        var xato = Assert.Throws<ArgumentException>(() => SmenaHisoblagich.NarxOzgarishi(smena, 13_000, [aparat], bir, K((1, 1099.99m)), Namuna.Vaqt()));
        Assert.Contains("1 100.00", xato.Message);
    }

    [Fact]
    public void NarxOzgarishi_OldingiKorsatkichBilanBirXil_SegmentYozilmaydi()
    {
        var smena = new Smena { Id = 3 };
        var aparat = Bitta();

        var yangi = SmenaHisoblagich.NarxOzgarishi(smena, 12_200, [aparat], [], K((1, 1000.00m)), Namuna.Vaqt());

        Assert.Empty(yangi);
        var yopish = SmenaHisoblagich.Yop(smena, [aparat], new Dictionary<int, long> { [1] = 13_000 }, yangi, K((1, 1010m)), 0, 0, 0, null, Bosh, Namuna.Vaqt());
        Assert.Equal(130_000, yopish.Natija.Savdo);             // hammasi yangi narxda
    }

    [Fact]
    public void NarxOzgarishi_YopilganSmenaVaBoshqaYoqilgiAparati_RadEtiladi()
    {
        var yopilgan = new Smena { Id = 3, Tugadi = Namuna.Vaqt() };
        Assert.Throws<InvalidOperationException>(() => SmenaHisoblagich.NarxOzgarishi(yopilgan, 12_200, [Bitta()], [], K((1, 1100m)), Namuna.Vaqt()));

        var ochiq = new Smena { Id = 4 };
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.NarxOzgarishi(ochiq, 12_200, [Bitta()], [], K((1, 1100m), (99, 5m)), Namuna.Vaqt()));
    }

    [Fact]
    public void Oldingi_SegmentBoLsaOxirgisiningOxiri_AksHoldaTotalLitr()
    {
        var aparat = Bitta();
        Assert.Equal(1000.00m, SmenaHisoblagich.Oldingi(aparat, []));
        var segmentlar = new[]
        {
            new SmenaKorsatkichi { AparatId = 1, Tartib = 1, Oxiri = 1100m },
            new SmenaKorsatkichi { AparatId = 1, Tartib = 2, Oxiri = 1150m },
        };
        Assert.Equal(1150m, SmenaHisoblagich.Oldingi(aparat, segmentlar));
    }
}
