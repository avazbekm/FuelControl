namespace FuelControl.Core.Xizmatlar;

/// <summary>Vaqt bazada UTC; kunlik/oylik hisob va ko'rsatishda Toshkent (UTC+5, yozgi vaqt yo'q).</summary>
public static class Vaqt
{
    public static readonly TimeSpan Toshkent = TimeSpan.FromHours(5);

    /// <summary>Toshkent kuni boshlanishi (UTC sifatida).</summary>
    public static DateTime KunBoshi(DateTime utc) => utc.Add(Toshkent).Date.Subtract(Toshkent);

    /// <summary>UTC vaqtning Toshkent sanasi (smena ochilgan sana, "bugun" va h.k.).</summary>
    public static DateOnly Sana(DateTime utc) => DateOnly.FromDateTime(utc.Add(Toshkent));

    /// <summary>Toshkent oyi boshlanishi (UTC sifatida).</summary>
    public static DateTime OyBoshi(DateTime utc)
    {
        var m = utc.Add(Toshkent);
        return DateTime.SpecifyKind(new DateTime(m.Year, m.Month, 1) - Toshkent, DateTimeKind.Utc);
    }

    /// <summary>Keyingi Toshkent oyi boshlanishi (UTC sifatida).</summary>
    public static DateTime KeyingiOyBoshi(DateTime utc)
    {
        var m = utc.Add(Toshkent);
        return DateTime.SpecifyKind(new DateTime(m.Year, m.Month, 1).AddMonths(1) - Toshkent, DateTimeKind.Utc);
    }

    /// <summary>?dan=2026-03-01 kabi Toshkent sanasini UTC chegaraga aylantiradi.</summary>
    public static DateTime? Dan(DateOnly? d) => d is null ? null : DateTime.SpecifyKind(d.Value.ToDateTime(TimeOnly.MinValue) - Toshkent, DateTimeKind.Utc);

    public static DateTime? Gacha(DateOnly? d) => d is null ? null : DateTime.SpecifyKind(d.Value.AddDays(1).ToDateTime(TimeOnly.MinValue) - Toshkent, DateTimeKind.Utc);
}
