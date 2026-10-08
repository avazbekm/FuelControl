using System;
using System.Collections.Generic;
using System.Linq;
using FuelControl.Contracts.Dto;

namespace FuelControl.Desktop.Services;

/// <summary>
/// Nasiya / mijoz qidiruvi (§8.3) — server bilan bir xil qoida:
/// ism — katta-kichik harf farqlanmaydi, apostroflar solishtirishda butunlay tashlanadi ("Toxtayev" "To'xtayev"ni topadi),
/// so'z boshi ham, ichidagi qism ham mos;
/// telefon — faqat raqamlar (kamida 3), "+998", bo'shliq va chiziqlar e'tiborga olinmaydi;
/// mashina raqami — bo'shliqsiz, katta harfda. Tartib: aynan va boshidan mos kelganlar oldin, keyin qolganlari, yangisi oldinda.
/// </summary>
public static class NasiyaQidiruv
{
    private static readonly char[] Apostroflar = ['\'', '‘', '’', '`', 'ʻ', 'ʼ', '´'];

    /// <summary>Apostroflar butunlay tashlanadi: "Toxtayev", "To'xtayev" va "TO`XTAYEV" bir-birini topadi.</summary>
    public static string Ism(string? s) =>
        new string((s ?? "").Trim().ToLowerInvariant().Where(c => Array.IndexOf(Apostroflar, c) < 0).ToArray());

    public static string Raqam(string? s) => new string((s ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    /// <summary>Qidiruvdagi telefon raqamlari: "+998" va boshidagi 998 tashlanadi (TelefonRaqamlari bilan bir xil).</summary>
    private static string TelefonQ(string q) => new string(q.Where(char.IsDigit).ToArray()) is var r && r.StartsWith("998") && r.Length > 3 ? r[3..] : new string(q.Where(char.IsDigit).ToArray());

    /// <summary>0 — mos emas, 2 — aynan yoki boshidan mos, 1 — ichida mos.</summary>
    public static int Moslik(string mijozIsmi, string telefon, string mashinaRaqami, string q)
    {
        q = q.Trim();
        if (q.Length == 0) return 1;
        var natija = 0;

        var qi = Ism(q);
        var ism = Ism(mijozIsmi);
        if (ism.Length > 0)
        {
            if (ism.StartsWith(qi) || ism.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(s => s.StartsWith(qi))) natija = 2;
            else if (ism.Contains(qi)) natija = Math.Max(natija, 1);
        }

        var qt = TelefonQ(q);
        if (qt.Length >= 3 && q.All(c => char.IsDigit(c) || c is '+' or ' ' or '-' or '(' or ')'))
        {
            var tel = Format.TelefonRaqamlari(telefon);
            if (tel.StartsWith(qt)) natija = 2;
            else if (tel.Contains(qt)) natija = Math.Max(natija, 1);
        }

        var qr = Raqam(q);
        var raqam = Raqam(mashinaRaqami);
        if (qr.Length > 0 && raqam.Length > 0)
        {
            if (raqam.StartsWith(qr)) natija = 2;
            else if (raqam.Contains(qr)) natija = Math.Max(natija, 1);
        }
        return natija;
    }

    public static int Moslik(NasiyaDto n, string q) => Moslik(n.MijozIsmi, n.Telefon, n.MashinaRaqami, q);
    public static int Moslik(MijozTaklifDto m, string q) => Moslik(m.MijozIsmi, m.Telefon, m.MashinaRaqami, q);

    /// <summary>Filtr + tartib: avval boshidan mos kelganlar, keyin ichida mos kelganlar; har birida yangisi oldinda.</summary>
    public static IEnumerable<NasiyaDto> Filtrla(IEnumerable<NasiyaDto> royxat, string q)
    {
        q = q.Trim();
        if (q.Length == 0) return royxat;
        return royxat.Select(n => (n, m: Moslik(n, q))).Where(x => x.m > 0)
            .OrderByDescending(x => x.m).ThenByDescending(x => x.n.Yozildi).Select(x => x.n);
    }
}
