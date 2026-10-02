using FuelControl.Contracts;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class RuxsatXizmatiTests
{
    [Fact]
    public void Operator_FaqatSotuvVaSmenaRuxsatlariga_Ega()
    {
        var ruxsatlar = RuxsatXizmati.Standart(Rol.Operator);
        Assert.Equal(
            new HashSet<Ruxsat> { Ruxsat.SotuvKiritish, Ruxsat.SmenaOchish, Ruxsat.SmenaYopish },
            ruxsatlar);
    }

    [Fact]
    public void Boshliq_SozlamalardanTashqariHammasigaEga()
    {
        var ruxsatlar = RuxsatXizmati.Standart(Rol.Boshliq);
        Assert.DoesNotContain(Ruxsat.Sozlamalar, ruxsatlar);
        Assert.Contains(Ruxsat.Boshqaruv, ruxsatlar);
        Assert.Contains(Ruxsat.SotuvTahrirlash, ruxsatlar);
    }

    [Fact]
    public void Admin_HammaRuxsatlargaEga()
    {
        var ruxsatlar = RuxsatXizmati.Standart(Rol.Admin);
        Assert.Equal(RuxsatXizmati.Hammasi.Length, ruxsatlar.Count);
        Assert.Contains(Ruxsat.Sozlamalar, ruxsatlar);
    }
}
