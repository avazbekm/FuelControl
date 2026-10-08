using System.Globalization;

namespace FuelControl.Core.Xizmatlar;

/// <summary>Foydalanuvchiga ko'rinadigan matnlar (audit, harakat izohi) uchun raqam formati: "1 500 000", "184 642.30".</summary>
public static class Format
{
    private static readonly NumberFormatInfo Nfi = new()
    {
        NumberGroupSeparator = " ", NumberDecimalSeparator = ".", NegativeSign = "-",
    };

    /// <summary>So'm: probel bilan guruhlangan butun son.</summary>
    public static string Pul(long n) => n.ToString("N0", Nfi);

    /// <summary>Litr yoki pult ko'rsatkichi: probel bilan guruhlangan, 2 xonali kasr.</summary>
    public static string Litr(decimal n) => n.ToString("N2", Nfi);

    /// <summary>Butun bo'lsa kasrsiz ("8 000"), bo'lmasa 2 xonali ("6 428.20"): bak va kirim matnlarida.</summary>
    public static string LitrIxcham(decimal n) => n == decimal.Truncate(n) ? n.ToString("N0", Nfi) : n.ToString("N2", Nfi);

    /// <summary>"kk.oo" (masalan, nasiya muddati "07.10 gacha").</summary>
    public static string KunOy(DateOnly d) => d.ToString("dd.MM", CultureInfo.InvariantCulture);
}
