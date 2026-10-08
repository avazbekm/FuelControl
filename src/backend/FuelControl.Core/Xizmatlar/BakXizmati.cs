using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

/// <summary>
/// Bak: har aparatning o'z baki, litrda. Kirim qo'shadi; smena yopilganda sotilgan litr ayriladi (<see cref="SmenaHisoblagich.Yop"/>);
/// qo'lda tuzatish - faqat Sozlamalarda, sabab bilan (auditga yoziladi).
/// </summary>
public static class BakXizmati
{
    /// <summary>Bakka kirim (zavoddan kelgan yoqilg'i). Vaqt berilmasa - hozir. Aparat qoldig'i yangilanadi.</summary>
    public static BakKirim Kirim(Aparat aparat, decimal litr, DateTime? vaqt, string? hujjat, string kim, DateTime hozirUtc)
    {
        litr = SmenaHisoblagich.Yaxlitla(litr);
        if (litr <= 0) throw new ArgumentException("Kirim litri musbat bo'lishi kerak.");
        if (vaqt is { } v && v > hozirUtc.AddDays(1)) throw new ArgumentException("Kirim vaqti kelajakda bo'lishi mumkin emas.");
        var oldin = aparat.BakQoldiq;
        aparat.BakQoldiq = oldin + litr;
        return new BakKirim
        {
            AparatId = aparat.Id, Litr = litr, QoldiqOldin = oldin, QoldiqKeyin = aparat.BakQoldiq,
            Vaqt = vaqt ?? hozirUtc, Hujjat = string.IsNullOrWhiteSpace(hujjat) ? null : hujjat.Trim(), KimYozdi = kim,
        };
    }

    /// <summary>Bak qoldig'ini qo'lda tuzatadi (o'lchov bo'yicha). Qiymat o'zgarmasa - null (hech narsa yozilmaydi).</summary>
    public static BakTuzatishi? Tuzat(Aparat aparat, decimal yangiQoldiq, string? sabab, string kim, DateTime vaqtUtc)
    {
        yangiQoldiq = SmenaHisoblagich.Yaxlitla(yangiQoldiq);
        if (yangiQoldiq < 0) throw new ArgumentException("Bak qoldig'i manfiy bo'lishi mumkin emas.");
        if (yangiQoldiq == aparat.BakQoldiq) return null;
        if (string.IsNullOrWhiteSpace(sabab)) throw new ArgumentException("Bak qoldig'ini tuzatish sababi majburiy.");
        var t = new BakTuzatishi
        {
            AparatId = aparat.Id, Vaqt = vaqtUtc, Oldin = aparat.BakQoldiq, Keyin = yangiQoldiq, Sabab = sabab.Trim(), KimYozdi = kim,
        };
        aparat.BakQoldiq = yangiQoldiq;
        return t;
    }
}
