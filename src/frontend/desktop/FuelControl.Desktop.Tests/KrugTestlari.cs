using FuelControl.Desktop.Services;
using Xunit;

namespace FuelControl.Desktop.Tests;

/// <summary>§8.16: pult krugi — umumiy litr ↔ pult + krug o'girish va krug aniqlash (spec misollari).</summary>
public class KrugTestlari
{
    private const decimal H = 10_000m;

    [Theory]
    [InlineData(10_560.52, 2, 560.52)]
    [InlineData(10_000.00, 2, 0.00)]      // T = H: pult 0.00, krug 2
    [InlineData(9_999.99, 1, 9_999.99)]
    [InlineData(0.00, 1, 0.00)]
    [InlineData(25_000.10, 3, 5_000.10)]
    public void Ajratish(decimal t, int krug, decimal pult)
    {
        Assert.Equal(krug, Krug.Raqam(t, H));
        Assert.Equal(pult, Krug.Pult(t, H));
        Assert.Equal(t, Krug.Umumiy(pult, krug, H));
    }

    [Fact]
    public void Misol1_ShuKrugda()
    {
        // Oldingi 10 560.52 (560.52, krug 2), yozildi 1 250.30 → umumiy 11 250.30, 689.78 L
        var k = Krug.Ol(1_250.30m, 10_560.52m, H)!;
        Assert.Equal(11_250.30m, k.Umumiy);
        Assert.False(k.Oshdi);
        Assert.Equal(2, k.KrugOldin);
        Assert.Equal(2, k.KrugKeyin);
        Assert.Equal(689.78m, SmenaHisobi.Litr(10_560.52m, k.Umumiy));
    }

    [Fact]
    public void Misol2_KrugOshdi()
    {
        // Oldingi 9 800.00 (krug 1), yozildi 350.00 → umumiy 10 350.00, krug 2, 550.00 L
        var k = Krug.Ol(350m, 9_800m, H)!;
        Assert.Equal(10_350m, k.Umumiy);
        Assert.True(k.Oshdi);
        Assert.Equal(1, k.KrugOldin);
        Assert.Equal(2, k.KrugKeyin);
        Assert.Equal(550m, SmenaHisobi.Litr(9_800m, k.Umumiy));
    }

    [Fact]
    public void TengQiymat_KrugOzgarmaydi()
    {
        var k = Krug.Ol(560.52m, 10_560.52m, H)!;
        Assert.Equal(10_560.52m, k.Umumiy);
        Assert.False(k.Oshdi);
    }

    [Fact]
    public void AsosKrugChegarasida()
    {
        // Oldingi 10 000.00 = 0.00, krug 2: istalgan p ≥ 0 shu krugda
        var k = Krug.Ol(12.5m, 10_000m, H)!;
        Assert.Equal(10_012.5m, k.Umumiy);
        Assert.False(k.Oshdi);
    }

    [Theory]
    [InlineData(10_000.00)]   // p = H
    [InlineData(10_560.52)]   // operator 10 000 ni qo'lda qo'shgan
    [InlineData(-1.00)]
    public void ChegaradanTashqari_Null(decimal p)
    {
        Assert.Null(Krug.Ol(p, 9_800m, H));
    }

    [Fact]
    public void Krugsiz_QiymatOzi()
    {
        var k = Krug.Ol(10_560.52m, 9_800m, null)!;
        Assert.Equal(10_560.52m, k.Umumiy);
        Assert.False(k.Oshdi);
        Assert.Null(Krug.Hajm(0m));
        Assert.Null(Krug.Hajm(null));
    }

    [Fact]
    public void TasdiqQatoriMatni()
    {
        var k = Krug.Ol(350m, 9_800m, H)!;
        Assert.Equal("5-aparat: pult 0 dan qayta boshlangan, krug 1 → 2, sotilgan 550.00 L",   // Til.F birlik oldiga bo'linmaydigan bo'shliq qo'yadi
            Krug.TasdiqQatori("5-aparat", k, SmenaHisobi.Litr(9_800m, k.Umumiy)));
    }

    [Fact]
    public void BirlikSongaYopishgan()
    {
        Assert.Equal("9 710.00 L · krug 3", Krug.ToliqLitr(29_710m, H));
        Assert.Equal("9 710.00 L", Krug.ToliqLitr(9_710m, null));
    }

    [Fact]
    public void MatndanKiritish()
    {
        Assert.Equal(11_250.30m, Krug.Kirit("1 250.30", 10_560.52m, H)!.Umumiy);
        Assert.True(Krug.ChegaraXato("10 000", H));
        Assert.True(Krug.ChegaraXato("12 345.67", H));
        Assert.False(Krug.ChegaraXato("9 999.99", H));
        Assert.False(Krug.ChegaraXato("12 345.67", null));
        Assert.Null(Krug.Kirit("", 9_800m, H));
    }
}
