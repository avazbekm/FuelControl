using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(999, "999")]
    [InlineData(1_500_000, "1 500 000")]
    [InlineData(-45_520, "-45 520")]
    [InlineData(16_468_520, "16 468 520")]
    public void Pul_ProbelBilanGuruhlanadi(long n, string kutilgan) => Assert.Equal(kutilgan, Format.Pul(n));

    [Theory]
    [InlineData(184_642.30, "184 642.30")]
    [InlineData(1_236.8, "1 236.80")]
    [InlineData(0, "0.00")]
    [InlineData(62_946.9, "62 946.90")]
    public void Litr_IkkiXonaliKasr(double n, string kutilgan) => Assert.Equal(kutilgan, Format.Litr((decimal)n));

    [Fact]
    public void KunOy_KkOo() => Assert.Equal("07.10", Format.KunOy(new DateOnly(2026, 10, 7)));
}
