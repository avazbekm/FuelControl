using System;

namespace FuelControl.Desktop.Services;

/// <summary>
/// Sahifada o'z xato maydoni bo'lmagan amallar uchun (smena yopish, narx, ruxsat, zaxira, eksport) — xabar MainWindow'dagi
/// umumiy dialogda ko'rsatiladi (MainViewModel shu hodisaga obuna).
/// </summary>
public static class Bildirish
{
    /// <summary>(sarlavha, matn, xatomi)</summary>
    public static event Action<string, string, bool>? Korsatildi;

    public static void Xato(string xabar) => Korsatildi?.Invoke(Til.T("XatoSarlavha"), xabar, true);

    public static void Malumot(string xabar) => Korsatildi?.Invoke(Til.T("Malumot"), xabar, false);
}
