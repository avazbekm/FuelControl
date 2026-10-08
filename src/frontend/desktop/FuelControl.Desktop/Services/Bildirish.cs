using System;
using System.Threading.Tasks;

namespace FuelControl.Desktop.Services;

/// <summary>
/// Sahifada o'z xato maydoni bo'lmagan amallar uchun (narx, ruxsat, zaxira, eksport, o'chirish) — xabar MainWindow'dagi
/// umumiy dialogda ko'rsatiladi (MainViewModel shu hodisalarga obuna).
/// </summary>
public static class Bildirish
{
    /// <summary>(sarlavha, matn, xatomi)</summary>
    public static event Action<string, string, bool>? Korsatildi;

    /// <summary>(sarlavha, matn, tasdiq tugmasi matni, xavflimi, javob)</summary>
    public static event Action<string, string, string, bool, TaskCompletionSource<bool>>? TasdiqSoraldi;

    public static void Xato(string xabar) => Korsatildi?.Invoke(Til.T("XatoSarlavha"), xabar, true);

    public static void Malumot(string xabar) => Korsatildi?.Invoke(Til.T("Malumot"), xabar, false);

    /// <summary>"Ha / Bekor qilish" so'rovi. Obunachi bo'lmasa (masalan, sinovda) — true.</summary>
    public static Task<bool> Tasdiqla(string sarlavha, string matn, string tugma, bool xavfli = false)
    {
        if (TasdiqSoraldi is null) return Task.FromResult(true);
        var t = new TaskCompletionSource<bool>();
        TasdiqSoraldi.Invoke(sarlavha, matn, tugma, xavfli, t);
        return t.Task;
    }
}
