using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class XavfsizlikTests
{
    [Fact]
    public void Xesh_TogriParolniTasdiqlaydi()
    {
        var xesh = ParolXeshlash.Xeshla("1234");
        Assert.True(ParolXeshlash.Tekshir("1234", xesh));
        Assert.False(ParolXeshlash.Tekshir("4321", xesh));
    }

    [Fact]
    public void Xesh_HarSafarBoshqaTuzBilanYaratiladi()
    {
        Assert.NotEqual(ParolXeshlash.Xeshla("1234"), ParolXeshlash.Xeshla("1234"));
    }

    [Fact]
    public void Xesh_BuzilganQatordaFalseQaytaradi()
    {
        Assert.False(ParolXeshlash.Tekshir("1234", "buzuq"));
        Assert.False(ParolXeshlash.Tekshir("1234", "x.!!!.???"));
    }

    [Fact]
    public void BeshXatodanKeyinBlokLanadi()
    {
        var f = new Foydalanuvchi();
        var hozir = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 4; i++) KirishXizmati.XatoUrinish(f, hozir);
        Assert.False(KirishXizmati.Blokmi(f, hozir));

        KirishXizmati.XatoUrinish(f, hozir);
        Assert.True(KirishXizmati.Blokmi(f, hozir));
        Assert.True(KirishXizmati.Blokmi(f, hozir.AddMinutes(14)));
        Assert.False(KirishXizmati.Blokmi(f, hozir.AddMinutes(16)));
    }

    [Fact]
    public void MuvaffaqiyatliKirishXatolarniTozalaydi()
    {
        var f = new Foydalanuvchi { XatoUrinishlar = 3 };
        KirishXizmati.Muvaffaqiyatli(f);
        Assert.Equal(0, f.XatoUrinishlar);
        Assert.Null(f.BlokGacha);
    }
}

public class OylikMaoshXizmatiTests
{
    [Fact]
    public void FaqatYozilmaganOperatorlargaMaoshYoziladi()
    {
        var hozir = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var oyBoshi = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var operatorlar = new[]
        {
            new Foydalanuvchi { Id = 1, OylikMaosh = 4_000_000 },
            new Foydalanuvchi { Id = 2, OylikMaosh = 5_000_000 },
        };
        var mavjud = new[]
        {
            new HisobHarakati { OperatorId = 1, Turi = HarakatTuri.Maosh, Sana = oyBoshi },
        };

        var yozuvlar = OylikMaoshXizmati.KerakliYozuvlar(operatorlar, mavjud, hozir).ToList();

        var y = Assert.Single(yozuvlar);
        Assert.Equal(2, y.OperatorId);
        Assert.Equal(5_000_000, y.Summa);
        Assert.Equal(HarakatTuri.Maosh, y.Turi);
        Assert.Equal(oyBoshi, y.Sana);
    }

    [Fact]
    public void OtganOydagiMaoshHisobgaOlinmaydi()
    {
        var hozir = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var operatorlar = new[] { new Foydalanuvchi { Id = 1, OylikMaosh = 1_000_000 } };
        var otgan = new[]
        {
            new HisobHarakati { OperatorId = 1, Turi = HarakatTuri.Maosh, Sana = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
        };

        Assert.Single(OylikMaoshXizmati.KerakliYozuvlar(operatorlar, otgan, hozir));
    }
}
