namespace FuelControl.Contracts.Dto;

public sealed record AparatDto(int Id, int Raqam, int YoqilgiTuriId, string YoqilgiNomi, decimal TotalLitr);

public sealed record AparatYaratishDto(int Raqam, int YoqilgiTuriId, decimal BoshlangichTotalLitr);

/// <summary>TotalLitr berilsa va farq qilsa — totalizator tuzatiladi (auditga yoziladi).</summary>
public sealed record AparatTahrirlashDto(int Raqam, int YoqilgiTuriId, decimal? TotalLitr = null);
