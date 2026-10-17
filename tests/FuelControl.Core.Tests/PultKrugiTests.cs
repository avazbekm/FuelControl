using System.Globalization;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>Pult krugi (docs 8.16): krug hajmini saqlanadigan ko'rinishga keltirish. Hisob formulalari umumiy litrda qoladi - ular boshqa testlarda.</summary>
public class PultKrugiTests
{
    private static decimal? D(string? s) => s is null ? null : decimal.Parse(s, CultureInfo.InvariantCulture);

    [Theory]
    [InlineData(null, null)]
    [InlineData("0", null)]
    [InlineData("0.00", null)]
    [InlineData("0.004", null)]            // 2 xonaga yaxlitlanganda 0 - krug yo'q
    [InlineData("0.005", "0.01")]          // AwayFromZero
    [InlineData("10000", "10000")]
    [InlineData("10000.00", "10000.00")]
    [InlineData("10000.004", "10000.00")]
    [InlineData("10000.005", "10000.01")]
    [InlineData("9999.99", "9999.99")]
    public void Normallashtir_NullVa0_KrugYoq_MusbatYaxlitlanadi(string? kirish, string? kutilgan) =>
        Assert.Equal(D(kutilgan), PultKrugi.Normallashtir(D(kirish)));

    [Theory]
    [InlineData("-1")]
    [InlineData("-0.001")]                 // yaxlitlanganda 0 bo'lsa ham manfiy - 400
    [InlineData("-10000")]
    public void Normallashtir_Manfiy_ArgumentException400(string kirish)
    {
        var xato = Assert.Throws<ArgumentException>(() => PultKrugi.Normallashtir(D(kirish)));
        Assert.Equal("Krug hajmi manfiy bo'lmasligi kerak.", xato.Message);
    }
}
