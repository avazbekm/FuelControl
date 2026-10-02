namespace FuelControl.Contracts.Dto;

public sealed record YoqilgiTuriDto(int Id, string Nomi, long Narx, string Rang, bool AparatgaBiriktirilgan);

public sealed record YoqilgiYaratishDto(string Nomi, long Narx, string Rang);

public sealed record YoqilgiTahrirlashDto(string Nomi, long Narx, string Rang);

public sealed record NarxTarixiDto(DateTime Vaqt, string Yoqilgi, long EskiNarx, long YangiNarx, string Kim);
