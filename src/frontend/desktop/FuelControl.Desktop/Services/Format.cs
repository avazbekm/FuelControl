using System.Globalization;

namespace FuelControl.Desktop.Services;

public static class Format
{
    private static readonly NumberFormatInfo Nf = new() { NumberGroupSeparator = " ", NumberDecimalSeparator = "." };

    /// <summary>5845940 → "5 845 940"</summary>
    public static string Pul(long summa) => summa.ToString("#,0", Nf);

    /// <summary>5845940 → "5 845 940 so'm"</summary>
    public static string Som(long summa) => Pul(summa) + " " + Til.T("Som");

    /// <summary>Farq: +120 000 / −80 000</summary>
    public static string Farq(long summa) => summa switch
    {
        > 0 => "+" + Pul(summa),
        < 0 => "−" + Pul(-summa),
        _ => "0",
    };

    public static string Litr(decimal litr) => litr.ToString("#,0.00", Nf) + " " + Til.T("L");

    public static string Sana(System.DateTime d) => d.ToString("dd.MM.yyyy");
    public static string Vaqt(System.DateTime d) => d.ToString("HH:mm");
    public static string SanaVaqt(System.DateTime d) => d.ToString("dd.MM.yyyy HH:mm");
}
