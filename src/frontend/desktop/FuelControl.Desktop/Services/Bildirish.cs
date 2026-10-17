using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FuelControl.Desktop.Services;

/// <summary>Tasdiq so'rovi. Bekor — bekor qilish tugmasi matni (null — "Bekor qilish"); BekorFokus — fokus bekor tugmasida, Enter saqlamaydi.</summary>
public sealed record TasdiqSorovi(string Sarlavha, string Matn, string Tugma, bool Xavfli, string? Bekor, bool BekorFokus, TaskCompletionSource<bool> Javob);

/// <summary>
/// Sahifada o'z xato maydoni bo'lmagan amallar uchun (narx, ruxsat, zaxira, eksport, o'chirish) — xabar MainWindow'dagi
/// umumiy dialogda ko'rsatiladi (MainViewModel shu hodisalarga obuna).
/// </summary>
public static class Bildirish
{
    /// <summary>(sarlavha, matn, xatomi)</summary>
    public static event Action<string, string, bool>? Korsatildi;

    public static event Action<TasdiqSorovi>? TasdiqSoraldi;

    public static void Xato(string xabar) => Korsatildi?.Invoke(Til.T("XatoSarlavha"), xabar, true);

    public static void Malumot(string xabar) => Korsatildi?.Invoke(Til.T("Malumot"), xabar, false);

    /// <summary>"Ha / Bekor qilish" so'rovi. Obunachi bo'lmasa (masalan, sinovda) — true.</summary>
    public static Task<bool> Tasdiqla(string sarlavha, string matn, string tugma, bool xavfli = false, string? bekor = null, bool bekorFokus = false)
    {
        if (TasdiqSoraldi is null) return Task.FromResult(true);
        var t = new TaskCompletionSource<bool>();
        TasdiqSoraldi.Invoke(new TasdiqSorovi(sarlavha, matn, tugma, xavfli, bekor, bekorFokus, t));
        return t.Task;
    }

    /// <summary>
    /// §8.16: pult krugi oshgan aparatlar bo'lsa — "Tekshiraman" (fokus, false) / "Ha, to'g'ri" (true). Bo'sh ro'yxat — so'ramaydi.
    /// Tartib: krug tasdig'i → bak tasdig'i (§8.15) → "Smenani yopasizmi?".
    /// </summary>
    public static Task<bool> KrugTasdiqla(IEnumerable<string> qatorlar)
    {
        var l = qatorlar.ToList();
        if (l.Count == 0) return Task.FromResult(true);
        return Tasdiqla(Til.T("Krug_TasdiqSarlavha"), string.Join("\n", l.Select(q => "• " + q)), Til.T("Krug_HaTogri"),
            bekor: Til.T("BakOgoh_Tekshiraman"), bekorFokus: true);
    }

    /// <summary>
    /// §8.15: bakdagidan ko'p litr chiqadigan aparatlar bo'lsa — "Tekshiraman" (fokus, false) / "Baribir saqlash" (true).
    /// Ro'yxat bo'sh bo'lsa so'ramaydi (true). Bloklamaydi: bak qoldig'i eskirgan bo'lishi mumkin.
    /// </summary>
    public static Task<bool> BakTasdiqla(IEnumerable<string> qatorlar)
    {
        var l = qatorlar.ToList();
        if (l.Count == 0) return Task.FromResult(true);
        var matn = $"{Til.T("BakOgoh_Savol")}\n{string.Join("\n", l.Select(q => "• " + q))}\n\n{Til.T("BakOgoh_Eslatma")}";
        return Tasdiqla(Til.T("BakOgoh_Sarlavha"), matn, Til.T("BakOgoh_BaribirSaqlash"), bekor: Til.T("BakOgoh_Tekshiraman"), bekorFokus: true);
    }
}
