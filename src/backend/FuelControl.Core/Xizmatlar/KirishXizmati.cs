using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

/// <summary>Login urinishlari: ketma-ket 5 xato → 15 daqiqa blok.</summary>
public static class KirishXizmati
{
    public const int MaksimalXatoUrinish = 5;
    public static readonly TimeSpan BlokMuddati = TimeSpan.FromMinutes(15);

    public static bool Blokmi(Foydalanuvchi f, DateTime hozirUtc) => f.BlokGacha is { } b && b > hozirUtc;

    public static void XatoUrinish(Foydalanuvchi f, DateTime hozirUtc)
    {
        f.XatoUrinishlar++;
        if (f.XatoUrinishlar >= MaksimalXatoUrinish)
        {
            f.BlokGacha = hozirUtc + BlokMuddati;
            f.XatoUrinishlar = 0;
        }
    }

    public static void Muvaffaqiyatli(Foydalanuvchi f)
    {
        f.XatoUrinishlar = 0;
        f.BlokGacha = null;
    }
}
