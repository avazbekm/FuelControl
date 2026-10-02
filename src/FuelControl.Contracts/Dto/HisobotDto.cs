namespace FuelControl.Contracts.Dto;

/// <summary>
/// Hisobot qatori. Guruh — tilga bog'liq emas: operator ismi / kun "yyyy-MM-dd" / oy "yyyy-MM" (klient o'z tilida formatlaydi).
/// Har guruh ichida yoqilg'i bo'yicha qatorlar (Yoqilgi = nomi, Jami = false), oxirida guruh jami (Yoqilgi = null, Jami = true).
/// Kamomat, Avans, BekorSoni — faqat guruh jami qatorida (yoqilg'iga bog'liq emas), yoqilg'i qatorlarida 0.
/// </summary>
public sealed record HisobotQatoriDto(
    string Guruh,
    string? Yoqilgi,
    decimal Litr,
    long Summa,
    long Naqd,
    long Plastik,
    long Click,
    int Soni,
    long Kamomat,
    long Avans,
    int BekorSoni,
    bool Jami);

public sealed record HisobotDto(HisobotQatoriDto[] Qatorlar, HisobotQatoriDto Jami);
