namespace FuelControl.Contracts.Dto;

/// <summary>
/// Hisobot qatori (faqat yopilgan smenalar). Guruh tilga bog'liq emas: smena -> "41", kun -> "yyyy-MM-dd", oy -> "yyyy-MM", operator -> ism.
/// Sana: smena/kun uchun shu sana (Toshkent, smena ochilgan sana), oy/operator uchun null. OperatorIsmi: smena uchun smena operatori, boshqalarda null.
/// SmenaSoni - qatordagi smenalar soni. NaqdSavdo = Savdo - Plastik - Depozit(farqi) - Nasiya. Kamomat/Ortiqcha - musbat sonlar.
/// XarajatSoni - qatordagi smenalarda yozilgan xarajatlar soni (Xarajat - ularning summasi).
/// </summary>
public sealed record HisobotQatoriDto(string Guruh, DateOnly? Sana, string? OperatorIsmi, int SmenaSoni, decimal Litr,
    long Savdo, long Plastik, long Depozit, long Nasiya, long QaytganNasiya, long Xarajat, long NaqdSavdo,
    long Kamomat, long Ortiqcha, bool Jami, int XarajatSoni);

/// <summary>Davr uchun aparat/bak jadvali (litrda): BakBoshida + Kirim - Sotildi = BakOxirida.</summary>
public sealed record HisobotAparatDto(int AparatId, int Raqam, string YoqilgiNomi,
    decimal BakBoshida, decimal Kirim, decimal Sotildi, decimal BakOxirida, long Savdo);

/// <summary>Avans - davrdagi avanslar yig'indisi (musbat son).</summary>
public sealed record HisobotDto(HisobotQatoriDto[] Qatorlar, HisobotQatoriDto Jami, HisobotAparatDto[] Aparatlar, long Avans);
