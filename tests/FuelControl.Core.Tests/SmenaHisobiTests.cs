using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class SmenaHisobiTests
{
    [Fact]
    public void DizaynNamunasi_Kutilgan4923520_Sanalgan4878000_Kamomat45520()
    {
        var smena = Namuna.OchiqSmena();
        var aparatlar = Namuna.Aparatlar();

        var yopish = Namuna.Yop(smena, aparatlar);

        var n = yopish.Natija;
        Assert.Equal(16_468_520, n.Savdo);
        Assert.Equal(1236.80m, n.JamiLitr);
        Assert.Equal(7_630_000, n.Plastik);          // 7 830 000 - 200 000
        Assert.Equal(2_160_000, n.DepozitFarqi);     // 3 410 000 - 1 250 000
        Assert.Equal(570_000, n.NasiyaJami);
        Assert.Equal(300_000, n.QaytganNasiya);
        Assert.Equal(1_585_000, n.XarajatJami);
        Assert.Equal(4_923_520, n.Kutilgan);
        Assert.Equal(-45_520, n.Farq);

        // Natija smena yozuviga ham saqlangan, smena yopilgan.
        Assert.False(smena.Ochiqmi);
        Assert.Equal(Namuna.Vaqt(), smena.Tugadi);
        Assert.Equal((7_830_000L, 3_410_000L, 4_878_000L), (smena.YopishTerminal, smena.YopishDepozit, smena.SanalganNaqd));
        Assert.Equal((4_923_520L, -45_520L, 16_468_520L), (smena.Kutilgan, smena.Farq, smena.Savdo));
        Assert.Equal(SmenaHisoblagich.Saqlangan(smena), n);

        // Kamomat operator hisobiga (oylikdan ayiriladi): Smena #42.
        var h = Assert.IsType<HisobHarakati>(yopish.Harakat);
        Assert.Equal((HarakatTuri.Kamomat, -45_520L, 2, "Smena #42"), (h.Turi, h.Summa, h.OperatorId, h.Izoh));
    }

    [Fact]
    public void DizaynNamunasi_HarAparatSegmentiVaBakQoldigi()
    {
        var smena = Namuna.OchiqSmena();
        var aparatlar = Namuna.Aparatlar();

        var yopish = Namuna.Yop(smena, aparatlar);

        Assert.Equal([5_023_960L, 2_993_880L, 2_766_750L, 1_356_250L, 4_327_680L], yopish.YangiSegmentlar.Select(x => x.Summa));
        Assert.Equal([411.80m, 245.40m, 178.50m, 87.50m, 313.60m], yopish.YangiSegmentlar.Select(x => x.Litr));
        Assert.All(yopish.YangiSegmentlar, x => Assert.False(x.NarxOzgarishida));
        // Pult ko'rsatkichi yangi qiymatga, bak qoldig'idan sotilgan litr ayrildi (dizayndagi "Aparatlar holati").
        Assert.Equal([184_642.30m, 97_655.40m, 63_296.70m, 41_990.25m, 120_873.60m], aparatlar.Select(a => a.TotalLitr));
        Assert.Equal([6_428.20m, 2_969.60m, 3_941.50m, 1_872.50m, 9_166.40m], aparatlar.Select(a => a.BakQoldiq));
    }

    [Fact]
    public void KassadaNaqdTogri_FarqNol_HarakatYoq()
    {
        var yopish = Namuna.Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), sanalgan: 4_923_520);
        Assert.Equal(0, yopish.Natija.Farq);
        Assert.Null(yopish.Harakat);
    }

    [Fact]
    public void Ortiqcha_MusbatFarq_OrtiqchaHarakati()
    {
        var yopish = Namuna.Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), sanalgan: 4_943_520);
        Assert.Equal(20_000, yopish.Natija.Farq);
        var h = Assert.IsType<HisobHarakati>(yopish.Harakat);
        Assert.Equal((HarakatTuri.Ortiqcha, 20_000L), (h.Turi, h.Summa));
    }

    [Fact]
    public void TekshiruvHolati_DepozitKamayadi_KamomatEmas()
    {
        // Tekshiruv paytida naqd olinib, depozit kartadan terminal orqali yechildi: DepozitFarqi manfiy, kutilgan naqd oshadi.
        var smena = new Smena { Id = 7, OperatorId = 1, OchishQaytim = 100_000, OchishTerminal = 0, OchishDepozit = 1_000_000 };
        var aparat = new Aparat { Id = 1, Raqam = 1, YoqilgiTuriId = 1, TotalLitr = 1000m, BakQoldiq = 500m };

        var yopish = SmenaHisoblagich.Yop(smena, [aparat], new Dictionary<int, long> { [1] = 10_000 }, [],
            new Dictionary<int, decimal> { [1] = 1100m }, 0, 700_000, 1_400_000, null, SmenaHisoblagich.Yigindilar.Bosh, Namuna.Vaqt());

        Assert.Equal(-300_000, yopish.Natija.DepozitFarqi);
        Assert.Equal(1_400_000, yopish.Natija.Kutilgan);   // 100 000 + 1 000 000 + 300 000
        Assert.Equal(0, yopish.Natija.Farq);
        Assert.Null(yopish.Harakat);
    }

    /// <summary>Bitta aparatli ixcham smena: qaytim 100 000, depozit 1 000 000, savdo 500 000 (50 L x 10 000), terminal 0.</summary>
    private static SmenaHisoblagich.Natija Ixcham(long yopishTerminal, long yopishDepozit, long qaytgan, long xarajat, long sanalgan = 0)
    {
        var smena = new Smena { Id = 9, OperatorId = 1, OchishQaytim = 100_000, OchishTerminal = 0, OchishDepozit = 1_000_000 };
        var aparat = new Aparat { Id = 1, Raqam = 1, YoqilgiTuriId = 1, TotalLitr = 2000m, BakQoldiq = 100m };
        return SmenaHisoblagich.Yop(smena, [aparat], new Dictionary<int, long> { [1] = 10_000 }, [],
            new Dictionary<int, decimal> { [1] = 2050m }, yopishTerminal, yopishDepozit, sanalgan, null,
            new SmenaHisoblagich.Yigindilar(0, qaytgan, xarajat), Namuna.Vaqt()).Natija;
    }

    [Fact]
    public void DepozitdanXarajat_KassagaTasiriNol_KassadanXarajatKassaniKamaytiradi()
    {
        var xarajatsiz = Ixcham(0, 1_000_000, 0, 0);
        var depozitdan = Ixcham(0, 920_000, 0, 80_000);     // 80 000 depozit kartadan to'landi: karta qoldig'i kamaydi
        var kassadan = Ixcham(0, 1_000_000, 0, 80_000);     // 80 000 kassadan berildi

        Assert.Equal(600_000, xarajatsiz.Kutilgan);
        Assert.Equal(600_000, depozitdan.Kutilgan);          // kutilgan naqdga ta'siri 0
        Assert.Equal(520_000, kassadan.Kutilgan);
        Assert.Equal(80_000, depozitdan.XarajatJami);        // lekin xarajat sifatida hisobga olinadi
    }

    [Fact]
    public void QarzQaytishi_UchUsul_NaqdKassaniOshiradi_PlastikVaDepozitOshirmaydi()
    {
        var asos = Ixcham(0, 1_000_000, 0, 0);
        var naqd = Ixcham(0, 1_000_000, 200_000, 0);          // naqd kassaga tushdi
        var plastik = Ixcham(200_000, 1_000_000, 200_000, 0); // terminalga tushdi: terminal 200 000 ga oshdi
        var depozit = Ixcham(0, 1_200_000, 200_000, 0);       // depozit kartaga tushdi: karta 200 000 ga oshdi

        Assert.Equal(asos.Kutilgan + 200_000, naqd.Kutilgan);
        Assert.Equal(asos.Kutilgan, plastik.Kutilgan);
        Assert.Equal(asos.Kutilgan, depozit.Kutilgan);
        Assert.All(new[] { naqd, plastik, depozit }, x => Assert.Equal(200_000, x.QaytganNasiya));
    }

    [Fact]
    public void KichikKorsatkich_RadEtiladi_SmenaVaAparatOzgarmaydi()
    {
        var smena = Namuna.OchiqSmena();
        var aparatlar = Namuna.Aparatlar();
        var korsatkichlar = Namuna.YangiKorsatkichlar();
        korsatkichlar[3] = 63_118.19m;                        // oldingisi 63 118.20

        var xato = Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Yop(smena, aparatlar, Namuna.Narxlar, [], korsatkichlar,
            7_830_000, 3_410_000, 4_878_000, null, Namuna.Yig, Namuna.Vaqt()));

        Assert.Contains("3-aparat", xato.Message);
        Assert.True(smena.Ochiqmi);
        Assert.Equal(63_118.20m, aparatlar[2].TotalLitr);
        Assert.Equal(6_840m, aparatlar[0].BakQoldiq);
    }

    [Fact]
    public void TengKorsatkich_LitrNol_Mumkin()
    {
        var smena = Namuna.OchiqSmena();
        var aparatlar = Namuna.Aparatlar();
        var korsatkichlar = Namuna.YangiKorsatkichlar();
        korsatkichlar[5] = 120_560.00m;                       // dizel aparati smenada ishlamagan

        var yopish = SmenaHisoblagich.Yop(smena, aparatlar, Namuna.Narxlar, [], korsatkichlar, 7_830_000, 3_410_000, 4_878_000, null, Namuna.Yig, Namuna.Vaqt());

        Assert.Equal(0m, yopish.YangiSegmentlar.Single(x => x.AparatId == 5).Litr);
        Assert.Equal(16_468_520 - 4_327_680, yopish.Natija.Savdo);
    }

    [Fact]
    public void YetishmayotganYokiNomalumAparat_ManfiyPul_YopilganSmena_RadEtiladi()
    {
        var korsatkichlar = Namuna.YangiKorsatkichlar();
        korsatkichlar.Remove(2);
        var yetishmaydi = Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), Namuna.Narxlar, [],
            korsatkichlar, 7_830_000, 3_410_000, 4_878_000, null, Namuna.Yig, Namuna.Vaqt()));
        Assert.Contains("2-aparat", yetishmaydi.Message);

        var ortiqcha = Namuna.YangiKorsatkichlar();
        ortiqcha[99] = 1m;
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), Namuna.Narxlar, [], ortiqcha,
            7_830_000, 3_410_000, 4_878_000, null, Namuna.Yig, Namuna.Vaqt()));

        foreach (var (t, d, n) in new[] { (-1L, 0L, 0L), (0L, -1L, 0L), (0L, 0L, -1L) })
            Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), Namuna.Narxlar, [],
                Namuna.YangiKorsatkichlar(), t, d, n, null, Namuna.Yig, Namuna.Vaqt()));

        var yopilgan = Namuna.OchiqSmena();
        Namuna.Yop(yopilgan, Namuna.Aparatlar());
        Assert.Throws<InvalidOperationException>(() => Namuna.Yop(yopilgan, Namuna.Aparatlar()));
    }

    [Theory]
    [InlineData(0.05, 10_010, 501)]       // 500.5 -> 501 (AwayFromZero)
    [InlineData(0.05, 10_000, 500)]
    [InlineData(1.00, 12_200, 12_200)]
    [InlineData(0.00, 12_200, 0)]
    [InlineData(33.33, 12_200, 406_626)]
    public void Summa_SomgachaYaxlitlanadi(double litr, long narx, long kutilgan) =>
        Assert.Equal(kutilgan, SmenaHisoblagich.Summa((decimal)litr, narx));

    [Fact]
    public void Och_ManfiyQoldiqRadEtiladi_YozuvToldiriladi()
    {
        var s = SmenaHisoblagich.Och(3, 100_000, 200_000, 1_250_000, Namuna.Vaqt());
        Assert.Equal((3, 100_000L, 200_000L, 1_250_000L, true), (s.OperatorId, s.OchishQaytim, s.OchishTerminal, s.OchishDepozit, s.Ochiqmi));
        Assert.Equal(Namuna.Vaqt(), s.Boshlandi);
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Och(3, -1, 0, 0, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Och(3, 0, -1, 0, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => SmenaHisoblagich.Och(3, 0, 0, -1, Namuna.Vaqt()));
    }

    [Fact]
    public void SmenaYigindilari_FaqatShuSmenaHisobigaYozilganlar()
    {
        var nasiyalar = new[] { new Nasiya { SmenaId = 5, Summa = 350_000 }, new Nasiya { SmenaId = 5, Summa = 220_000 }, new Nasiya { SmenaId = 4, Summa = 99 } };
        var qaytishlar = new[] { new NasiyaQaytishi { SmenaId = 5, Summa = 300_000 }, new NasiyaQaytishi { SmenaId = null, Summa = 50_000 }, new NasiyaQaytishi { SmenaId = 4, Summa = 7 } };
        var xarajatlar = new[] { new Xarajat { SmenaId = 5, Summa = 1_500_000 }, new Xarajat { SmenaId = 5, Summa = 85_000, Manba = XarajatManbai.Depozit } };

        var y = SmenaHisoblagich.SmenaYigindilari(5, nasiyalar, qaytishlar, xarajatlar);

        Assert.Equal(new SmenaHisoblagich.Yigindilar(570_000, 300_000, 1_585_000), y);
    }
}
