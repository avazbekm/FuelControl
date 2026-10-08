using System.Text;
using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

/// <summary>
/// Nasiya qidiruvi - GET /nasiyalar?q= va GET /nasiyalar/mijozlar?q= uchun yagona qoida:
/// ism - katta-kichik harfsiz, apostroflar butunlay tashlab yuboriladi ("Toxtayev" va "To'xtayev" bir-birini topadi; so'rovda ham,
/// ismda ham), so'z boshi ham ichidagi qism ham mos; telefon - faqat raqamlar
/// (+998, bo'shliq va chiziqlar e'tiborga olinmaydi), kamida 3 raqam; mashina raqami - bo'shliqsiz va katta harfda.
/// Daraja (tartib uchun): 0 - aynan mos, 1 - boshidan (maydon yoki ismdagi so'z boshidan), 2 - ichidan.
/// </summary>
public sealed class NasiyaQidiruvi
{
    /// <summary>Telefon bo'yicha qidirish uchun kamida shuncha raqam (mamlakat kodisiz) yozilishi kerak.</summary>
    public const int MinTelefonRaqami = 3;

    private const string MamlakatKodi = "998";

    // ' ‘ ’ ` ʻ ʼ ´: o'/g' belgisi uchun yoziladigan apostroflar. Odamlar ularni ko'pincha yozmaydi, shuning uchun solishtirishda
    // butunlay tashlab yuboriladi ("o'xshash ismlar chiqsin"): "Toxtayev", "To'xtayev", "TO`XTAYEV", "Ulugbek", "Ulug'bek".
    private static readonly char[] Apostroflar = ['\'', '\u2018', '\u2019', '`', '\u02BB', '\u02BC', '\u00B4'];

    private readonly string _ism, _telefon, _raqam;

    private NasiyaQidiruvi(string ism, string telefon, string raqam)
    {
        _ism = ism;
        _telefon = telefon;
        _raqam = raqam;
    }

    /// <summary>So'rov bo'sh (normallashtirishdan keyin ham: faqat bo'shliq/apostroflar) - hamma nasiya mos (daraja 0).</summary>
    public bool Bosh => _ism.Length == 0;

    public static NasiyaQidiruvi Tayyorla(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return new("", "", "");
        var raqamlar = TelefonRaqami.Raqamlar(q);
        // So'rovdagi raqamlarning boshidagi 998 - mamlakat kodi: "+998 90 123" ham "90 123" kabi qidiriladi, "+998" yolg'iz o'zi - hech narsa.
        if (raqamlar.StartsWith(MamlakatKodi, StringComparison.Ordinal)) raqamlar = raqamlar[MamlakatKodi.Length..];
        return new(IsmKaliti(q), raqamlar.Length >= MinTelefonRaqami ? raqamlar : "", RaqamKaliti(q));
    }

    /// <summary>Ism kaliti: kichik harf, apostroflar olib tashlangan, bo'shliqlar bittadan, chetlari kesilgan ("Ulug'bek  Nazarov" -> "ulugbek nazarov").</summary>
    public static string IsmKaliti(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        var bosh = false;
        foreach (var c in s.Trim())
        {
            if (char.IsWhiteSpace(c)) { bosh = true; continue; }
            if (Array.IndexOf(Apostroflar, c) >= 0) continue;
            if (bosh && sb.Length > 0) sb.Append(' ');
            bosh = false;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Mashina raqami kaliti: faqat harf va raqamlar, katta harfda ("01 a 777 bc" -> "01A777BC").</summary>
    public static string RaqamKaliti(string? s) =>
        s is null ? "" : new string(s.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Nasiya so'rovga mosmi: null - mos emas; aks holda ism, telefon va mashina raqami bo'yicha eng yaxshi (eng kichik) daraja.</summary>
    public int? Daraja(Nasiya n)
    {
        if (Bosh) return 0;
        int? eng = null;
        foreach (var d in new[]
        {
            Moslik(IsmKaliti(n.MijozIsmi), _ism, sozBoshi: true),
            Moslik(TelefonRaqami.Milliy(n.Telefon), _telefon, sozBoshi: false),
            Moslik(RaqamKaliti(n.MashinaRaqami), _raqam, sozBoshi: false),
        })
            if (d is { } v && (eng is null || v < eng)) eng = v;
        return eng;
    }

    private static int? Moslik(string maydon, string sorov, bool sozBoshi)
    {
        if (sorov.Length == 0 || maydon.Length == 0) return null;
        if (maydon == sorov) return 0;
        if (maydon.StartsWith(sorov, StringComparison.Ordinal) || (sozBoshi && maydon.Contains(' ' + sorov, StringComparison.Ordinal))) return 1;
        return maydon.Contains(sorov, StringComparison.Ordinal) ? 2 : null;
    }
}
