namespace FuelControl.Core.Xizmatlar;

/// <summary>
/// Pult krugi (docs 8.16): ba'zi pultlar 10 000 da 0 ga qaytadi va "krug" hisoblagichi bittaga oshadi. Dastur ichida va API'da ko'rsatkich doim
/// UMUMIY litr (TotalLitr, segmentlarning Boshi/Oxiri, AparatKorsatkichDto.Qiymat); krug hajmi (Aparat.KrugHajmi) faqat klientda ko'rsatish va
/// kiritish qoidasi uchun saqlanadi: litr, summa, bak va hisobot formulalari o'zgarmaydi.
/// </summary>
public static class PultKrugi
{
    /// <summary>
    /// Krug hajmini saqlanadigan ko'rinishga keltiradi (2 xonagacha yaxlitlanadi): null va 0 — krug yo'q (null); musbat — hajmning o'zi.
    /// Manfiy bo'lsa ArgumentException (API'da 400). "O'zgarmaydi" (tahrirlashda null) ma'nosini chaqiruvchi o'zi ajratadi: bu yerda null va 0 bir xil.
    /// </summary>
    public static decimal? Normallashtir(decimal? hajm)
    {
        if (hajm is not { } h) return null;
        if (h < 0) throw new ArgumentException("Krug hajmi manfiy bo'lmasligi kerak.");
        var q = SmenaHisoblagich.Yaxlitla(h);
        return q == 0 ? null : q;
    }
}
