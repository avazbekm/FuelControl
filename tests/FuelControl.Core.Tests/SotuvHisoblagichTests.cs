using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class SotuvHisoblagichTests
{
    [Theory]
    [InlineData(100_000, 12_200, 8.20)]
    [InlineData(50_000, 15_500, 3.23)]
    public void SummadanLitr_TogriYaxlitlaydi(long summa, long narx, decimal kutilgan)
    {
        Assert.Equal(kutilgan, SotuvHisoblagich.SummadanLitr(summa, narx));
    }

    [Theory]
    [InlineData(8.20, 12_200, 100_040)]
    [InlineData(3.23, 15_500, 50_065)]
    public void LitrdanSumma_TogriYaxlitlaydi(decimal litr, long narx, long kutilgan)
    {
        Assert.Equal(kutilgan, SotuvHisoblagich.LitrdanSumma(litr, narx));
    }

    [Fact]
    public void SummadanLitr_NolNarxdaXatoBeradi()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SotuvHisoblagich.SummadanLitr(1000, 0));
    }
}
