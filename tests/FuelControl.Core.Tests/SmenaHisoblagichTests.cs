using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class SmenaHisoblagichTests
{
    private static Sotuv Sotuv(decimal litr, params (TolovTuri, long)[] tolovlar) => new()
    {
        Litr = litr,
        Summa = tolovlar.Sum(t => t.Item2),
        Tolovlar = tolovlar.Select(t => new SotuvTolovi { Turi = t.Item1, Summa = t.Item2 }).ToList(),
        Holati = SotuvHolati.Faol,
    };

    [Fact]
    public void QaytaHisobla_TolovTurlariBoyichaAjratadi()
    {
        var smena = new Smena();
        var sotuvlar = new List<Sotuv>
        {
            Sotuv(5m, (TolovTuri.Naqd, 60_000)),
            Sotuv(3m, (TolovTuri.Plastik, 36_000)),
            Sotuv(2m, (TolovTuri.Naqd, 10_000), (TolovTuri.Click, 14_000)),
        };

        SmenaHisoblagich.QaytaHisobla(smena, sotuvlar);

        Assert.Equal(3, smena.SotuvSoni);
        Assert.Equal(10m, smena.JamiLitr);
        Assert.Equal(70_000, smena.KutilganNaqd);
        Assert.Equal(36_000, smena.KutilganPlastik);
        Assert.Equal(14_000, smena.KutilganClick);
    }

    [Fact]
    public void Yop_Kamomat_FarqManfiyBoladi()
    {
        var smena = new Smena { KutilganNaqd = 100_000, KutilganPlastik = 0, KutilganClick = 0 };

        SmenaHisoblagich.Yop(smena, naqd: 90_000, plastik: 0, click: 0, izoh: null, vaqtUtc: DateTime.UtcNow);

        Assert.Equal(-10_000, SmenaHisoblagich.Farq(smena));
        Assert.Equal(10_000, SmenaHisoblagich.Kamomat(smena));
        Assert.Equal(0, SmenaHisoblagich.Ortiqcha(smena));
    }

    [Fact]
    public void Yop_Ortiqcha_FarqMusbatBoladi()
    {
        var smena = new Smena { KutilganNaqd = 100_000 };

        SmenaHisoblagich.Yop(smena, naqd: 105_000, plastik: 0, click: 0, izoh: null, vaqtUtc: DateTime.UtcNow);

        Assert.Equal(5_000, SmenaHisoblagich.Farq(smena));
        Assert.Equal(5_000, SmenaHisoblagich.Ortiqcha(smena));
        Assert.Equal(0, SmenaHisoblagich.Kamomat(smena));
    }

    [Fact]
    public void Yop_AllaqachonYopilganSmenaniQaytaYopishXatoBeradi()
    {
        var smena = new Smena { Tugadi = DateTime.UtcNow };

        Assert.Throws<InvalidOperationException>(() =>
            SmenaHisoblagich.Yop(smena, 1000, 0, 0, null, DateTime.UtcNow));
    }

    [Fact]
    public void OchiqSmenadaFarqNolga_Teng()
    {
        var smena = new Smena { KutilganNaqd = 50_000 };
        Assert.Equal(0, SmenaHisoblagich.Farq(smena));
    }
}
