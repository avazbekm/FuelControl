using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>Yopishda plastik bir nechta summa bilan kiritiladi (docs 8.9): saqlanadi, tekshiriladi, formula o'zgarmaydi.</summary>
public class PlastikSummalariTests
{
    private static SmenaHisoblagich.YopishNatijasi Yop(Smena smena, List<Aparat> aparatlar, long terminal, IReadOnlyList<long>? plastik) =>
        SmenaHisoblagich.Yop(smena, aparatlar, Namuna.Narxlar, [], Namuna.YangiKorsatkichlar(), terminal, 3_410_000, 4_878_000, null, Namuna.Yig, Namuna.Vaqt(), plastik);

    private static Smena OchishTerminalsiz() { var s = Namuna.OchiqSmena(); s.OchishTerminal = 0; return s; }

    [Fact]
    public void RoyxatBilanYopish_Saqlanadi_FormulaOzgarmaydi()
    {
        var smena = Namuna.OchiqSmena();

        var yopish = Yop(smena, Namuna.Aparatlar(), 7_830_000, [7_000_000, 580_000, 250_000]);

        Assert.Equal([7_000_000L, 580_000L, 250_000L], smena.PlastikSummalari);
        Assert.Equal(7_830_000, smena.YopishTerminal);
        // Plastik = Terminal - OchishTerminal (200 000); kutilgan naqd va farq ro'yxatsiz yopish bilan aynan bir xil.
        Assert.Equal(7_630_000, yopish.Natija.Plastik);
        Assert.Equal(Namuna.Yop(Namuna.OchiqSmena(), Namuna.Aparatlar()).Natija, yopish.Natija);
        Assert.Equal(-45_520, yopish.Natija.Farq);
    }

    [Fact]
    public void RoyxatsizYopish_EskiKlient_BoshMassiv_NatijaOzgarmagan()
    {
        var smena = Namuna.OchiqSmena();

        var yopish = Yop(smena, Namuna.Aparatlar(), 7_830_000, null);

        Assert.Empty(smena.PlastikSummalari);
        Assert.Equal(7_830_000, smena.YopishTerminal);
        Assert.Equal(Namuna.Yop(Namuna.OchiqSmena(), Namuna.Aparatlar()).Natija, yopish.Natija);
    }

    [Theory]
    [InlineData(7_830_001L, new long[] { 7_000_000, 830_000 })]       // yig'indi 7 830 000, terminal 7 830 001
    [InlineData(7_830_000L, new long[] { 7_000_000, 500_000 })]       // yig'indi kam
    [InlineData(7_830_000L, new long[] { 7_830_000, 1 })]             // yig'indi ortiq
    [InlineData(7_830_000L, new long[0])]                              // bo'sh ro'yxat = yig'indi 0
    public void YigindiTerminalgaTengEmas_400_SmenaOchiqQoladi_HechNarsaOzgarmaydi(long terminal, long[] plastik)
    {
        var smena = Namuna.OchiqSmena();
        var aparatlar = Namuna.Aparatlar();
        var jami = plastik.Sum();

        var xato = Assert.Throws<ArgumentException>(() => Yop(smena, aparatlar, terminal, plastik));

        Assert.Equal($"Plastik summalari yig'indisi ({Format.Pul(jami)}) terminalga ({Format.Pul(terminal)}) teng bo'lishi kerak.", xato.Message);
        Assert.True(smena.Ochiqmi);
        Assert.Empty(smena.PlastikSummalari);
        Assert.Null(smena.YopishTerminal);
        Assert.Equal(184_230.50m, aparatlar[0].TotalLitr);               // aparat va bak ham tegilmagan
        Assert.Equal(6_840m, aparatlar[0].BakQoldiq);
    }

    [Theory]
    [InlineData(new long[] { 7_830_000, 100, -100 })]
    [InlineData(new long[] { -1, 7_830_001 })]
    public void ManfiySumma_Rad(long[] plastik)
    {
        var xato = Assert.Throws<ArgumentException>(() => Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), 7_830_000, plastik));

        Assert.Equal("Plastik summasi manfiy bo'lishi mumkin emas.", xato.Message);
    }

    [Fact]
    public void EngKopi20Ta_Qabul_21Ta_Rad()
    {
        var yigirma = Enumerable.Repeat(391_500L, 20).ToArray();                 // 20 x 391 500 = 7 830 000
        var smena = Namuna.OchiqSmena();
        Yop(smena, Namuna.Aparatlar(), 7_830_000, yigirma);
        Assert.Equal(yigirma, smena.PlastikSummalari);

        var yigirmaBir = Enumerable.Repeat(0L, 21).ToArray();
        var xato = Assert.Throws<ArgumentException>(() => Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), 0, yigirmaBir));
        Assert.Equal("Plastik summalari ko'pi bilan 20 ta bo'lishi mumkin (21 ta berilgan).", xato.Message);
    }

    [Fact]
    public void NolSummaliQatorlarMumkin_BoshRoyxatFaqatTerminalNolBolsa()
    {
        var a = OchishTerminalsiz();
        Yop(a, Namuna.Aparatlar(), 0, [0]);
        Assert.Equal([0L], a.PlastikSummalari);

        var b = OchishTerminalsiz();
        Yop(b, Namuna.Aparatlar(), 7_830_000, [0, 7_830_000, 0]);
        Assert.Equal([0L, 7_830_000L, 0L], b.PlastikSummalari);

        var v = OchishTerminalsiz();
        Yop(v, Namuna.Aparatlar(), 0, []);                                       // yig'indi 0 = terminal 0
        Assert.True(v.PlastikSummalari.Length == 0 && !v.Ochiqmi);
    }

    [Fact]
    public void YigindiLongdanOshsa_ArgumentException_OverflowEmas()
    {
        var xato = Assert.Throws<ArgumentException>(() => Yop(Namuna.OchiqSmena(), Namuna.Aparatlar(), 7_830_000, [long.MaxValue, 1]));

        Assert.Equal("Plastik summalari yig'indisi juda katta.", xato.Message);
    }

    [Fact]
    public void PlastikniTekshir_NullBoshMassiv_NusxaQaytaradi()
    {
        Assert.Empty(SmenaHisoblagich.PlastikniTekshir(null, 123));
        IReadOnlyList<long> manba = [100L, 23L];
        var nusxa = SmenaHisoblagich.PlastikniTekshir(manba, 123);
        Assert.Equal([100L, 23L], nusxa);
        Assert.NotSame(manba, nusxa);
    }

    [Fact]
    public void KorsatkichTuzatish_PlastikQismlariniOzgartirmaydi()
    {
        var smena = Namuna.OchiqSmena();
        var aparatlar = Namuna.Aparatlar();
        var yopish = Yop(smena, aparatlar, 7_830_000, [7_000_000, 830_000]);

        SmenaHisoblagich.KorsatkichniTuzat(smena, aparatlar[0], yopish.YangiSegmentlar, [], 184_652.30m, "Xato o'qilgan", "Boshliq", Namuna.Vaqt(14));

        Assert.Equal([7_000_000L, 830_000L], smena.PlastikSummalari);
        Assert.Equal(7_830_000, smena.YopishTerminal);
    }
}
