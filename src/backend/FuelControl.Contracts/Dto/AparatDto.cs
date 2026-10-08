namespace FuelControl.Contracts.Dto;

/// <summary>
/// TotalLitr — pultdagi "Total, L" (oxirgi yopilgan smena holatida). BakQoldiq — aparatning o'z baki, litrda:
/// kirimlar − sotilgan (smena yopilganda ayriladi). OxirgiKirim* — oxirgi bakka kirim (yo'q bo'lsa null).
/// </summary>
public sealed record AparatDto(int Id, int Raqam, int YoqilgiTuriId, string YoqilgiNomi, decimal TotalLitr,
    decimal BakQoldiq, DateTime? OxirgiKirimVaqti, decimal? OxirgiKirimLitr);

public sealed record AparatYaratishDto(int Raqam, int YoqilgiTuriId, decimal BoshlangichTotalLitr, decimal BoshlangichBakQoldiq);

/// <summary>TotalLitr yoki BakQoldiq berilsa va farq qilsa — qo'lda tuzatiladi: Sabab majburiy, auditga yoziladi.</summary>
public sealed record AparatTahrirlashDto(int Raqam, int YoqilgiTuriId, decimal? TotalLitr = null, decimal? BakQoldiq = null, string? Sabab = null);

/// <summary>Zavoddan kelgan yoqilg'i, litrda. Vaqt berilmasa — hozir. Hujjat — raqam yoki izoh.</summary>
public sealed record BakKirimYaratishDto(decimal Litr, DateTime? Vaqt, string? Hujjat);

public sealed record BakKirimDto(int Id, int AparatId, int AparatRaqam, decimal Litr, decimal QoldiqOldin, decimal QoldiqKeyin,
    DateTime Vaqt, string? Hujjat, string KimYozdi);
