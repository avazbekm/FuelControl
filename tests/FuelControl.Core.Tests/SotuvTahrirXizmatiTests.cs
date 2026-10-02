using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class SotuvTahrirXizmatiTests
{
    [Fact]
    public void Tahrirla_EskiVaYangiQiymatlarniQaytaradi()
    {
        var sotuv = new Sotuv
        {
            AparatId = 1,
            Narx = 12_200,
            Summa = 100_000,
            Litr = 8.20m,
            Tolovlar = new() { new SotuvTolovi { Turi = TolovTuri.Naqd, Summa = 100_000 } },
        };

        var natija = SotuvTahrirXizmati.Tahrirla(
            sotuv,
            yangiAparatId: 2,
            yangiNarx: 15_500,
            yangiSumma: 50_000,
            yangiTolovlar: new List<SotuvTolovi> { new() { Turi = TolovTuri.Plastik, Summa = 50_000 } });

        Assert.Equal(100_000, natija.EskiSumma);
        Assert.Equal(8.20m, natija.EskiLitr);
        Assert.Equal(1, natija.EskiAparatId);
        Assert.Equal(50_000, natija.YangiSumma);
        Assert.Equal(2, natija.YangiAparatId);
        Assert.Equal(SotuvHisoblagich.SummadanLitr(50_000, 15_500), natija.YangiLitr);

        // Sotuv obyekti ham yangilangan.
        Assert.Equal(2, sotuv.AparatId);
        Assert.Equal(50_000, sotuv.Summa);
        Assert.Single(sotuv.Tolovlar);
    }

    [Fact]
    public void Tahrirla_TolovlarYigindisiSummagaTengBolmasa_XatoBeradi()
    {
        var sotuv = new Sotuv { AparatId = 1, Narx = 12_200, Summa = 100_000, Litr = 8.20m };

        Assert.Throws<ArgumentException>(() => SotuvTahrirXizmati.Tahrirla(
            sotuv, 1, 12_200, 100_000,
            new List<SotuvTolovi> { new() { Turi = TolovTuri.Naqd, Summa = 90_000 } }));
    }

    [Fact]
    public void Tahrirla_BekorQilinganSotuvniTahrirlashXatoBeradi()
    {
        var sotuv = new Sotuv { Holati = SotuvHolati.BekorQilingan };

        Assert.Throws<InvalidOperationException>(() => SotuvTahrirXizmati.Tahrirla(
            sotuv, 1, 12_200, 100_000, new List<SotuvTolovi> { new() { Turi = TolovTuri.Naqd, Summa = 100_000 } }));
    }

    [Fact]
    public void BekorQil_HolatiniOzgartiradiVaSababniSaqlaydi()
    {
        var sotuv = new Sotuv { Holati = SotuvHolati.Faol };

        SotuvTahrirXizmati.BekorQil(sotuv, "Xato summa kiritilgan", "Bahodir Rahimov");

        Assert.Equal(SotuvHolati.BekorQilingan, sotuv.Holati);
        Assert.Equal("Xato summa kiritilgan", sotuv.BekorSababi);
        Assert.Equal("Bahodir Rahimov", sotuv.BekorQilgan);
    }

    [Fact]
    public void BekorQil_AllaqachonBekorQilinganniYanaBekorQilishXatoBeradi()
    {
        var sotuv = new Sotuv { Holati = SotuvHolati.BekorQilingan };
        Assert.Throws<InvalidOperationException>(() => SotuvTahrirXizmati.BekorQil(sotuv, "sabab", "kim"));
    }
}
