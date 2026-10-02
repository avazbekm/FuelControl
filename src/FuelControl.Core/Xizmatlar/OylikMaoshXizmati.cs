using FuelControl.Contracts;
using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

/// <summary>Oylik maosh har oy boshida avtomatik "Maosh" harakati sifatida yoziladi (Hangfire shart emas — har safar "shu oy uchun yozilganmi" tekshiriladi).</summary>
public static class OylikMaoshXizmati
{
    public static IEnumerable<HisobHarakati> KerakliYozuvlar(
        IEnumerable<Foydalanuvchi> faolOperatorlar,
        IEnumerable<HisobHarakati> shuOydagiMaoshHarakatlari,
        DateTime hozirUtc)
    {
        var oyBoshi = new DateTime(hozirUtc.Year, hozirUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var oy = Oy(hozirUtc);
        var yozilganlar = shuOydagiMaoshHarakatlari
            .Where(h => h.Turi == HarakatTuri.Maosh && (h.MaoshOyi == oy || (h.MaoshOyi is null && h.Sana >= oyBoshi)))
            .Select(h => h.OperatorId)
            .ToHashSet();

        foreach (var op in faolOperatorlar)
        {
            if (yozilganlar.Contains(op.Id)) continue;
            yield return new HisobHarakati
            {
                OperatorId = op.Id,
                Sana = oyBoshi,
                Turi = HarakatTuri.Maosh,
                Summa = op.OylikMaosh,
                Izoh = $"{oy} oyi uchun maosh",
                KimYozdi = "Tizim",
                MaoshOyi = oy,
            };
        }
    }

    public static string Oy(DateTime utc) => utc.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
}
