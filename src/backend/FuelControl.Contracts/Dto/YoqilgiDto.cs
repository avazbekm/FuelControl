namespace FuelControl.Contracts.Dto;

public sealed record YoqilgiTuriDto(int Id, string Nomi, long Narx, string Rang, bool AparatgaBiriktirilgan);

public sealed record YoqilgiYaratishDto(string Nomi, long Narx, string Rang);

/// <summary>
/// Ochiq smenada narx o'zgarsa, shu yoqilg'i barcha aparatlarining hozirgi pult ko'rsatkichi (Korsatkichlar) majburiy:
/// bo'lmasa 400 (ProblemDetails, extensions.kerakliAparatlar: int[]). Shu paytgacha sotilgan litr eski narxda alohida segment bo'ladi.
/// </summary>
public sealed record YoqilgiTahrirlashDto(string Nomi, long Narx, string Rang, AparatKorsatkichDto[]? Korsatkichlar = null);

public sealed record NarxTarixiDto(DateTime Vaqt, string Yoqilgi, long EskiNarx, long YangiNarx, string Kim);
