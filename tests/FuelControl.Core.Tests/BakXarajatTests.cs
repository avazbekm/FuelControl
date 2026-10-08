using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class BakXizmatiTests
{
    private static Aparat Bitta() => new() { Id = 1, Raqam = 1, YoqilgiTuriId = 1, TotalLitr = 100m, BakQoldiq = 1_000m };

    [Fact]
    public void Kirim_QoldiqOshadi_OldinVaKeyinSaqlanadi_PultOzgarmaydi()
    {
        var aparat = Bitta();
        var kirim = BakXizmati.Kirim(aparat, 5_000m, null, "  Hujjat 17 ", "Boshliq", Namuna.Vaqt());

        Assert.Equal(6_000m, aparat.BakQoldiq);
        Assert.Equal(100m, aparat.TotalLitr);                    // "Pult ko'rsatkichi o'zgarmaydi"
        Assert.Equal((1, 5_000m, 1_000m, 6_000m, "Hujjat 17", "Boshliq"), (kirim.AparatId, kirim.Litr, kirim.QoldiqOldin, kirim.QoldiqKeyin, kirim.Hujjat, kirim.KimYozdi));
        Assert.Equal(Namuna.Vaqt(), kirim.Vaqt);                 // vaqt berilmasa - hozir
    }

    [Fact]
    public void Kirim_BerilganVaqtSaqlanadi_BoshHujjatNull()
    {
        var vaqt = Namuna.Vaqt(3).AddDays(-1);
        var kirim = BakXizmati.Kirim(Bitta(), 12.345m, vaqt, " ", "Op", Namuna.Vaqt());
        Assert.Equal(vaqt, kirim.Vaqt);
        Assert.Null(kirim.Hujjat);
        Assert.Equal(12.35m, kirim.Litr);                        // 2 xonagacha yaxlitlanadi
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(0.004)]
    public void Kirim_MusbatBolmasaRadEtiladi(double litr)
    {
        var aparat = Bitta();
        Assert.Throws<ArgumentException>(() => BakXizmati.Kirim(aparat, (decimal)litr, null, null, "Op", Namuna.Vaqt()));
        Assert.Equal(1_000m, aparat.BakQoldiq);
    }

    [Fact]
    public void Kirim_KelajakdagiVaqtRadEtiladi()
    {
        Assert.Throws<ArgumentException>(() => BakXizmati.Kirim(Bitta(), 10m, Namuna.Vaqt().AddDays(2), null, "Op", Namuna.Vaqt()));
    }

    [Fact]
    public void Tuzat_SababMajburiy_OzgarmasaYozuvYoq_ManfiyRadEtiladi()
    {
        var aparat = Bitta();
        Assert.Throws<ArgumentException>(() => BakXizmati.Tuzat(aparat, 900m, " ", "Admin", Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => BakXizmati.Tuzat(aparat, -1m, "o'lchov", "Admin", Namuna.Vaqt()));
        Assert.Null(BakXizmati.Tuzat(aparat, 1_000m, null, "Admin", Namuna.Vaqt()));   // o'zgarmagan - sabab ham kerak emas

        var t = BakXizmati.Tuzat(aparat, 950m, " Chizg'ich bilan o'lchandi ", "Admin", Namuna.Vaqt());

        Assert.Equal(950m, aparat.BakQoldiq);
        Assert.Equal((1_000m, 950m, "Chizg'ich bilan o'lchandi", "Admin"), (t!.Oldin, t.Keyin, t.Sabab, t.KimYozdi));
    }
}

public class XarajatXizmatiTests
{
    [Fact]
    public void Yarat_ToliqMalumotBilan()
    {
        var x = XarajatXizmati.Yarat(42, 2, "Alisher", 85_000, "  Lampochka va tozalash vositasi ", XarajatManbai.Kassa, Namuna.Vaqt());
        Assert.Equal((42, 2, "Alisher", 85_000L, "Lampochka va tozalash vositasi", XarajatManbai.Kassa), (x.SmenaId, x.OperatorId, x.KimYozdi, x.Summa, x.Sabab, x.Manba));
    }

    [Theory]
    [InlineData(0, "Sabab", XarajatManbai.Kassa)]
    [InlineData(-10, "Sabab", XarajatManbai.Depozit)]
    [InlineData(1000, "  ", XarajatManbai.Kassa)]
    [InlineData(1000, null, XarajatManbai.Kassa)]
    [InlineData(1000, "Sabab", (XarajatManbai)7)]
    public void Yarat_NotogriQiymatRadEtiladi(long summa, string? sabab, XarajatManbai manba) =>
        Assert.Throws<ArgumentException>(() => XarajatXizmati.Yarat(1, 1, "Op", summa, sabab, manba, Namuna.Vaqt()));

    [Fact]
    public void Yarat_JudaUzunSababRadEtiladi() =>
        Assert.Throws<ArgumentException>(() => XarajatXizmati.Yarat(1, 1, "Op", 1000, new string('x', 201), XarajatManbai.Kassa, Namuna.Vaqt()));
}
