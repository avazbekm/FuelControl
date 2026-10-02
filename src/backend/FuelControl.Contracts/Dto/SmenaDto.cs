namespace FuelControl.Contracts.Dto;

public sealed record SmenaDto(
    int Id,
    int OperatorId,
    string OperatorIsmi,
    DateTime Boshlandi,
    DateTime? Tugadi,
    long KutilganNaqd,
    long KutilganPlastik,
    long KutilganClick,
    decimal JamiLitr,
    int SotuvSoni,
    long? TopshirilganNaqd,
    long? TopshirilganPlastik,
    long? TopshirilganClick,
    long Farq,
    long Kamomat,
    long Ortiqcha,
    string? Izoh);

/// <summary>Smena tafsiloti: smena va uning barcha sotuvlari (bekor qilinganlari ham, vaqt bo'yicha yangisi tepada).</summary>
public sealed record SmenaTafsilotDto(SmenaDto Smena, SotuvDto[] Sotuvlar);

public sealed record SmenaYopishDto(long Naqd, long Plastik, long Click, string? Izoh);
