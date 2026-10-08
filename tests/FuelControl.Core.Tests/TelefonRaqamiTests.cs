using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class TelefonRaqamiTests
{
    [Theory]
    [InlineData("90 123 45 67")]
    [InlineData("998901234567")]
    [InlineData("+998-90-123-45-67")]
    [InlineData("+998 90 123 45 67")]
    [InlineData("998 90 123 45 67")]
    [InlineData(" +998 (90) 123-45-67 ")]
    [InlineData("901234567")]
    [InlineData("tel: 90.123.45.67")]
    public void Normallashtir_IstalganKorinish_StandartFormatga(string kiritilgan) =>
        Assert.Equal("+998 90 123 45 67", TelefonRaqami.Normallashtir(kiritilgan));

    [Fact]
    public void Normallashtir_QattiqBoshliqVaQoshimchaBelgilar()
    {
        Assert.Equal("+998 90 123 45 67", TelefonRaqami.Normallashtir("+998" + (char)160 + "90" + (char)160 + "1234567"));
        Assert.Equal("+998 90 123 45 67", TelefonRaqami.Normallashtir("90" + (char)9 + "123" + (char)8211 + "45" + (char)8211 + "67"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+998")]
    [InlineData("+998 ")]       // kiritish maydonidagi o'chmaydigan prefiks - telefon kiritilmagan
    [InlineData("998")]
    public void Normallashtir_BoshQiymatVaFaqatPrefiks_BoshSatr(string? kiritilgan) =>
        Assert.Equal("", TelefonRaqami.Normallashtir(kiritilgan));

    [Theory]
    [InlineData("90 123 45 6")]            // 8 raqam
    [InlineData("+998 90 123 45 6")]
    [InlineData("901234567 8")]            // 10 raqam
    [InlineData("+998 90 123 45 678")]     // 13 raqam
    [InlineData("+99890123456")]           // 11 raqam
    [InlineData("12345")]
    [InlineData("+998 90")]
    [InlineData("abc")]
    [InlineData("+")]
    public void Normallashtir_AynanToqqizRaqamEmas_ArgumentException(string kiritilgan) =>
        Assert.Throws<ArgumentException>(() => TelefonRaqami.Normallashtir(kiritilgan));

    [Fact]
    public void Normallashtir_998BilanBoshlanganToqqizRaqam_MilliyRaqam_KodEmas()
    {
        // 99 operatorining 8 bilan boshlanadigan raqami: 9 raqamli qiymatdan "998" olib tashlanmaydi.
        Assert.Equal("+998 99 812 34 56", TelefonRaqami.Normallashtir("998123456"));
        Assert.Equal("+998 99 812 34 56", TelefonRaqami.Normallashtir("998998123456"));
        Assert.Equal("+998 99 812 34 56", TelefonRaqami.Normallashtir("+998 99 812 34 56"));
    }

    [Fact]
    public void Normallashtir_IdempotentVaSaqlanganQiymatniQaytaNormallashtirish()
    {
        var bir = TelefonRaqami.Normallashtir("90 123 45 67");
        Assert.Equal(bir, TelefonRaqami.Normallashtir(bir));
    }

    [Theory]
    [InlineData("+998 90 123 45 67", "901234567")]
    [InlineData("998901234567", "901234567")]         // eski, normallashtirilmagan
    [InlineData("90-123-45-67", "901234567")]
    [InlineData("998123456", "998123456")]            // 9 raqamli: kod emas
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Milliy_MamlakatKodisizRaqamlar(string? saqlangan, string kutilgan) =>
        Assert.Equal(kutilgan, TelefonRaqami.Milliy(saqlangan));
}
