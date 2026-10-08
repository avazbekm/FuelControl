using System;
using System.Globalization;
using System.Linq;

namespace FuelControl.Desktop.Services;

public static class Format
{
    /// <summary>Ming ajratgich — bo'linmaydigan bo'shliq (U+00A0): son qator oxirida hech qachon bo'linmaydi.</summary>
    public const string Ajratgich = "\u00A0";
    private static readonly NumberFormatInfo Nf = new() { NumberGroupSeparator = Ajratgich, NumberDecimalSeparator = "." };

    /// <summary>5845940 → "5 845 940"; manfiy → "−5 845 940".</summary>
    public static string Pul(long summa) => summa < 0 ? "−" + (-summa).ToString("#,0", Nf) : summa.ToString("#,0", Nf);

    /// <summary>5845940 → "5 845 940 so'm"</summary>
    public static string Som(long summa) => Pul(summa) + Ajratgich + Til.T("Som");

    /// <summary>Farq: +120 000 / −80 000</summary>
    public static string Farq(long summa) => summa switch
    {
        > 0 => "+" + Pul(summa),
        < 0 => "−" + Pul(-summa),
        _ => "0",
    };

    /// <summary>184230.5 → "184 230.50" (birliksiz).</summary>
    public static string Son(decimal litr) => litr < 0 ? "−" + (-litr).ToString("#,0.00", Nf) : litr.ToString("#,0.00", Nf);

    /// <summary>Butun litr: 6840 → "6 840" (bak qoldig'i).</summary>
    public static string ButunLitr(decimal litr) => litr < 0 ? "−" + Math.Round(-litr).ToString("#,0", Nf) : Math.Round(litr).ToString("#,0", Nf);

    public static string Litr(decimal litr) => Son(litr) + Ajratgich + Til.T("L");

    public static string Sana(DateTime d) => d.ToString("dd.MM.yyyy");
    public static string Sana(DateOnly d) => d.ToString("dd.MM.yyyy");
    public static string QisqaSana(DateTime d) => d.ToString("dd.MM");
    public static string QisqaSana(DateOnly d) => d.ToString("dd.MM");
    public static string Vaqt(DateTime d) => d.ToString("HH:mm");
    public static string SanaVaqt(DateTime d) => d.ToString("dd.MM.yyyy HH:mm");
    /// <summary>"04.10 08:02"</summary>
    public static string QisqaSanaVaqt(DateTime d) => d.ToString("dd.MM HH:mm");

    /// <summary>"9 soat 40 daqiqa" / "40 daqiqa".</summary>
    public static string Davomiylik(TimeSpan d)
    {
        if (d < TimeSpan.Zero) d = TimeSpan.Zero;
        var soat = (int)d.TotalHours;
        return soat > 0 ? Til.F("Boshqaruv_SoatDaqiqa", soat, d.Minutes) : Til.F("Boshqaruv_Daqiqa", d.Minutes);
    }

    // ================= Telefon (§8.1): "+998 XX XXX XX XX" =================

    public const string TelefonPrefiks = "+998 ";

    /// <summary>
    /// Abonent raqamlari (eng ko'pi 9): boshidagi "+998" prefiksi (yoki uning buzilgan qismi) tashlanadi, raqam bo'lmagan belgilar olinadi,
    /// joylashtirilgan to'liq raqamdagi boshlang'ich "998" ham tushiriladi. "+998 90 123-45-67", "998901234567", "90 123 45 67" → "901234567".
    /// </summary>
    public static string TelefonRaqamlari(string? s)
    {
        var t = (s ?? "").TrimStart();
        if (t.StartsWith("+998")) t = t[4..];
        else if (t.StartsWith('+'))
        {
            // Prefiks qismi o'chirilgan ("+99 90 ...") — birinchi bo'lak tashlanadi.
            var i = t.IndexOf(' ');
            if (i is > 0 and <= 4) t = t[i..];
        }
        var r = new string(t.Where(char.IsDigit).ToArray());
        while (r.Length > 9 && r.StartsWith("998")) r = r[3..];
        return r.Length > 9 ? r[..9] : r;
    }

    /// <summary>Kiritish maydoni matni: prefiks + guruhlangan raqamlar (to'liq bo'lmasa ham): "+998 90 12".</summary>
    public static string TelefonKiritish(string raqamlar)
    {
        var sb = new System.Text.StringBuilder(TelefonPrefiks);
        for (int i = 0; i < raqamlar.Length; i++)
        {
            if (i is 2 or 5 or 7) sb.Append(' ');
            sb.Append(raqamlar[i]);
        }
        return sb.ToString();
    }

    /// <summary>Saqlanadigan qiymat: 9 raqam — "+998 XX XXX XX XX", raqamsiz — "", aks holda null (to'liq emas).</summary>
    public static string? TelefonSaqlash(string? s)
    {
        var r = TelefonRaqamlari(s);
        return r.Length == 0 ? "" : r.Length == 9 ? TelefonKiritish(r) : null;
    }

    /// <summary>Ko'rsatish: 9 raqamli bo'lsa standart ko'rinishda, aks holda o'zgarishsiz.</summary>
    public static string Telefon(string? s)
    {
        var r = TelefonRaqamlari(s);
        return r.Length == 9 ? TelefonKiritish(r) : s ?? "";
    }

    /// <summary>"Narimonjon Abdullayev" → "NA".</summary>
    public static string BoshHarflar(string ism)
    {
        var q = ism.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return q.Length >= 2 ? $"{q[0][0]}{q[1][0]}".ToUpperInvariant() : ism.Length > 0 ? ism[..1].ToUpperInvariant() : "?";
    }

    /// <summary>Foydalanuvchi kiritgan pul: faqat raqamlar olinadi ("1 250 000" → 1250000). Bo'sh → null.</summary>
    public static long? PulOl(string? s)
    {
        var t = new string((s ?? "").Where(char.IsDigit).ToArray());
        return t.Length > 0 && long.TryParse(t, out var v) ? v : null;
    }

    /// <summary>Foydalanuvchi kiritgan litr ("184 812,40" → 184812.40). Bo'sh yoki noto'g'ri → null.</summary>
    public static decimal? KasrOl(string? s)
    {
        var t = (s ?? "").Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return t.Length > 0 && decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
