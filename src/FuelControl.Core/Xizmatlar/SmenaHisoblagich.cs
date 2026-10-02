using FuelControl.Contracts;
using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

public static class SmenaHisoblagich
{
    /// <summary>Smenaning kutilgan summalarini faol sotuvlar asosida qayta hisoblaydi (tahrir/bekor qilishdan keyin ham chaqiriladi).</summary>
    public static void QaytaHisobla(Smena smena, IReadOnlyCollection<Sotuv> faolSotuvlar)
    {
        smena.SotuvSoni = faolSotuvlar.Count;
        smena.JamiLitr = faolSotuvlar.Sum(s => s.Litr);
        smena.KutilganNaqd = faolSotuvlar.Sum(s => ToloviYigindisi(s, TolovTuri.Naqd));
        smena.KutilganPlastik = faolSotuvlar.Sum(s => ToloviYigindisi(s, TolovTuri.Plastik));
        smena.KutilganClick = faolSotuvlar.Sum(s => ToloviYigindisi(s, TolovTuri.Click));
    }

    public static long ToloviYigindisi(Sotuv sotuv, TolovTuri turi) =>
        sotuv.Tolovlar.Where(t => t.Turi == turi).Sum(t => t.Summa);

    public static long KutilganJami(Smena s) => s.KutilganNaqd + s.KutilganPlastik + s.KutilganClick;

    public static long TopshirilganJami(Smena s) => (s.TopshirilganNaqd ?? 0) + (s.TopshirilganPlastik ?? 0) + (s.TopshirilganClick ?? 0);

    /// <summary>Manfiy = kamomat, musbat = ortiqcha. Ochiq smenada 0.</summary>
    public static long Farq(Smena s) => s.Ochiqmi ? 0 : TopshirilganJami(s) - KutilganJami(s);

    public static long Kamomat(Smena s) => Farq(s) < 0 ? -Farq(s) : 0;

    public static long Ortiqcha(Smena s) => Farq(s) > 0 ? Farq(s) : 0;

    public static void Yop(Smena smena, long naqd, long plastik, long click, string? izoh, DateTime vaqtUtc)
    {
        if (!smena.Ochiqmi) throw new InvalidOperationException("Smena allaqachon yopilgan.");
        if (naqd < 0 || plastik < 0 || click < 0) throw new ArgumentException("Topshirilgan summalar manfiy bo'lishi mumkin emas.");

        smena.TopshirilganNaqd = naqd;
        smena.TopshirilganPlastik = plastik;
        smena.TopshirilganClick = click;
        smena.Izoh = izoh;
        smena.Tugadi = vaqtUtc;
    }
}
