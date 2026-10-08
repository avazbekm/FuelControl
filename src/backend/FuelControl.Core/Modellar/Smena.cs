namespace FuelControl.Core.Modellar;

/// <summary>
/// Smena = bir sutka, butun shoxobchada bir vaqtda faqat bitta ochiq smena. Hisoblangan maydonlar (JamiLitr ... Farq) smena
/// yopilganda (va ko'rsatkich tuzatilganda) saqlanadi; ochiq smenada 0. Formula: <see cref="Xizmatlar.SmenaHisoblagich"/>.
/// </summary>
public sealed class Smena
{
    public int Id { get; set; }
    public int OperatorId { get; set; }
    public DateTime Boshlandi { get; set; }
    public DateTime? Tugadi { get; set; }

    // Ochishda operator qo'lda kiritadi (oldingi smenadan avtomatik olinmaydi).
    public long OchishQaytim { get; set; }
    public long OchishTerminal { get; set; }
    public long OchishDepozit { get; set; }

    // Yopishda kiritiladi.
    public long? YopishTerminal { get; set; }
    public long? YopishDepozit { get; set; }
    public long? SanalganNaqd { get; set; }
    public string? Izoh { get; set; }

    // Yopishda hisoblanadi (server — yagona haqiqat manbai).
    public decimal JamiLitr { get; set; }
    public long Savdo { get; set; }
    public long Plastik { get; set; }
    public long DepozitFarqi { get; set; }
    public long NasiyaJami { get; set; }
    public long QaytganNasiya { get; set; }
    public long XarajatJami { get; set; }
    public long Kutilgan { get; set; }

    /// <summary>SanalganNaqd − Kutilgan: manfiy — kamomat, musbat — ortiqcha.</summary>
    public long Farq { get; set; }

    public bool Ochiqmi => Tugadi is null;
}

/// <summary>
/// Smenaning bitta aparat segmenti. Odatda aparatga bitta; ochiq smenada narx o'zgarsa, o'sha paytgacha bo'lgan litr eski narxda
/// alohida segment (NarxOzgarishida = true) bo'lib yoziladi, qolgani yopishda yangi narxda.
/// </summary>
public sealed class SmenaKorsatkichi
{
    public int Id { get; set; }
    public int SmenaId { get; set; }
    public int AparatId { get; set; }

    /// <summary>Aparat ichida 1 dan boshlab.</summary>
    public int Tartib { get; set; }
    public decimal Boshi { get; set; }
    public decimal Oxiri { get; set; }
    public long Narx { get; set; }
    public decimal Litr { get; set; }
    public long Summa { get; set; }
    public bool NarxOzgarishida { get; set; }
    public DateTime Vaqt { get; set; }
}
