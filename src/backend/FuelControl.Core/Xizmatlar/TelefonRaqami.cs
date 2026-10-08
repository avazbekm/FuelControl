namespace FuelControl.Core.Xizmatlar;

/// <summary>Telefon raqami: bazaga saqlanadigan va API qaytaradigan ko'rinish doim "+998 XX XXX XX XX".</summary>
public static class TelefonRaqami
{
    private const string MamlakatKodi = "998";
    private const int MilliyRaqamlar = 9;

    /// <summary>
    /// Istalgan ko'rinishdagi telefonni "+998 XX XXX XX XX" ga keltiradi: raqam bo'lmagan belgilar tashlanadi, mamlakat kodi (998) bilan
    /// boshlangan 12 raqamdan kod olib tashlanadi, aynan 9 raqam qolishi shart - aks holda ArgumentException (API: 400).
    /// Bo'sh qiymat va faqat "+998" (kiritish maydonidagi o'chmaydigan prefiks) - telefon kiritilmagan: "" qaytadi;
    /// raqamsiz matn ("abc", "+") berilgan, lekin noto'g'ri telefon hisoblanadi.
    /// </summary>
    public static string Normallashtir(string? kiritilgan)
    {
        if (string.IsNullOrWhiteSpace(kiritilgan)) return "";
        var r = Raqamlar(kiritilgan);
        if (r == MamlakatKodi) return "";
        // Faqat uzun qiymatdan kodni olamiz: "998123456" - 99 operatorining 9 raqamli milliy raqami, kod emas.
        r = Milliy(r);
        if (r.Length != MilliyRaqamlar)
            throw new ArgumentException("Telefon raqami noto'g'ri: +998 dan keyin aynan 9 ta raqam bo'lishi kerak (+998 XX XXX XX XX).");
        return $"+{MamlakatKodi} {r[..2]} {r[2..5]} {r[5..7]} {r[7..9]}";
    }

    /// <summary>Faqat raqamlar (ASCII 0-9); boshqa belgilar - bo'shliq, "+", "-", qavs, harflar - tashlanadi.</summary>
    public static string Raqamlar(string? s) => s is null ? "" : new string(s.Where(c => c is >= '0' and <= '9').ToArray());

    /// <summary>
    /// Telefonning mamlakat kodisiz raqamlari (qidiruv va mijozlarni guruhlash uchun): "+998 90 123 45 67" -> "901234567".
    /// Eski (normallashtirilmagan) qiymatlar uchun ham ishlaydi; kod faqat 9 tadan uzun raqamdan olinadi.
    /// </summary>
    public static string Milliy(string? telefon)
    {
        var r = Raqamlar(telefon);
        return r.Length > MilliyRaqamlar && r.StartsWith(MamlakatKodi, StringComparison.Ordinal) ? r[MamlakatKodi.Length..] : r;
    }
}
