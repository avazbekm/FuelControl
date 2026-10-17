using System;

namespace FuelControl.Desktop.Services;

/// <summary>Krugli pultga kiritilgan qiymat: umumiy litr, krug oshdimi (pult 0 dan qayta boshlangan), asos va natija kruglari.</summary>
public sealed record KrugKiritish(decimal Umumiy, bool Oshdi, int KrugOldin, int KrugKeyin);

/// <summary>
/// §8.16: pult krugi — pult H litrda 0 ga qaytadi (masalan 10 000), pultdagi "krug" bittaga oshadi.
/// Dastur ichida va API'da ko'rsatkich doim UMUMIY litr (T); krug faqat ko'rsatish va kiritish qoidasi:
/// krug = floor(T / H) + 1, pult = T − (krug − 1) × H. H null (yoki ≤ 0) — krug yo'q, pult = T.
/// </summary>
public static class Krug
{
    /// <summary>Haqiqiy krug hajmi: null yoki ≤ 0 — krug yo'q.</summary>
    public static decimal? Hajm(decimal? h) => h is > 0 ? h : null;

    public static int Raqam(decimal t, decimal h) => (int)Math.Floor(t / h) + 1;

    public static decimal Pult(decimal t, decimal h) => t - (Raqam(t, h) - 1) * h;

    /// <summary>Ko'rsatish: krugli aparatda pult qismi ("560.52"), aks holda umumiy qiymat.</summary>
    public static string Son(decimal t, decimal? h) => Hajm(h) is { } hv ? Format.Son(Pult(t, hv)) : Format.Son(t);

    /// <summary>"krug 2" (krugsiz aparatda null).</summary>
    public static string? Belgi(decimal t, decimal? h) => Hajm(h) is { } hv ? Til.F("Krug_Belgi", Raqam(t, hv)) : null;

    /// <summary>Bir qatorda: "560.52 · krug 2" (krugsiz — "10 560.52").</summary>
    public static string Toliq(decimal t, decimal? h) => Belgi(t, h) is { } b ? $"{Son(t, h)} · {b}" : Format.Son(t);

    /// <summary>Birlik bilan: son va birligi yonma-yon — "9 710.00 L · krug 3" (krugsiz — "9 710.00 L").</summary>
    public static string ToliqLitr(decimal t, decimal? h)
    {
        var son = $"{Son(t, h)}{Format.Ajratgich}L";   // kartadagi boshqa qiymatlar ("6 830 L") bilan bir xil
        return Belgi(t, h) is { } b ? $"{son} · {b}" : son;
    }

    /// <summary>
    /// Operator pultda ko'rgan qiymat p → umumiy litr. Asos B — qatorning boshlanishi (yopish/narx o'zgarishida "Oldingi",
    /// tuzatishlarda segment Boshi). kB = floor(B/H)+1, pB = B − (kB−1)×H:
    /// p ≥ pB — shu krug, umumiy = (kB−1)×H + p; p &lt; pB — pult 0 dan qayta boshlangan, umumiy = kB×H + p (krug +1).
    /// Bir kiritishda faqat bitta krug aniqlanadi. Krugsiz aparatda p o'zi umumiy. 0 ≤ p &lt; H bo'lmasa — null (maydonda xato).
    /// </summary>
    public static KrugKiritish? Ol(decimal p, decimal asos, decimal? h)
    {
        if (Hajm(h) is not { } hv) return new KrugKiritish(p, false, 0, 0);
        if (p < 0 || p >= hv) return null;
        var kB = Raqam(asos, hv);
        var pB = asos - (kB - 1) * hv;
        return p >= pB
            ? new KrugKiritish((kB - 1) * hv + p, false, kB, kB)
            : new KrugKiritish(kB * hv + p, true, kB, kB + 1);
    }

    /// <summary>Maydon matni → kiritish (bo'sh, noto'g'ri yoki krug chegarasidan tashqari — null).</summary>
    public static KrugKiritish? Kirit(string matn, decimal asos, decimal? h) => Format.KasrOl(matn) is { } p ? Ol(p, asos, h) : null;

    /// <summary>Krugli aparatda yozilgan son 0 ≤ p &lt; H oralig'idan tashqarida.</summary>
    public static bool ChegaraXato(string matn, decimal? h) => Hajm(h) is { } hv && Format.KasrOl(matn) is { } p && (p < 0 || p >= hv);

    /// <summary>"Pult ko'rsatkichi 10 000.00 dan kichik bo'lishi kerak".</summary>
    public static string ChegaraXatoMatn(decimal? h) => Til.F("Krug_XatoKatta", Format.Son(h ?? 0));

    /// <summary>Krug oshganda maydon ostidagi yozuv: "krug 3".</summary>
    public static string KeyingiBelgi(KrugKiritish? k) => k is { Oshdi: true } ? Til.F("Krug_Belgi", k.KrugKeyin) : "";

    /// <summary>Tasdiq qatori: "5-aparat: pult 0 dan qayta boshlangan, krug 1 → 2, sotilgan 550.00 L".</summary>
    public static string TasdiqQatori(string nomi, KrugKiritish k, decimal litr) =>
        Til.F("Krug_TasdiqQator", nomi, k.KrugOldin, k.KrugKeyin, Format.Son(litr));

    /// <summary>Sozlamalar: "Pult ko'rsatkichi" + "Krug" → umumiy = (krug − 1) × H + pult.</summary>
    public static decimal Umumiy(decimal pult, int krug, decimal h) => (krug - 1) * h + pult;
}
