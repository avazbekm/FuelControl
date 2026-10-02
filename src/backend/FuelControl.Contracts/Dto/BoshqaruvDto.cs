namespace FuelControl.Contracts.Dto;

/// <summary>Bugungi KPI. OyKamomat — shu oydagi Kamomat harakatlari (musbat son).</summary>
public sealed record BoshqaruvKpiDto(
    long BugungiSumma,
    decimal BugungiLitr,
    int SotuvSoni,
    int OchiqSmenalar,
    long KechagiSumma,
    long OyJami,
    long OyKamomat);

public sealed record TolovUlushiDto(TolovTuri Turi, long Summa);

/// <summary>Barcha yoqilg'i turlari (bugun sotilmaganlari ham — 0 bilan).</summary>
public sealed record YoqilgiUlushiDto(string Nomi, string Rang, decimal Litr, long Summa);

public sealed record KunlikDto(DateOnly Sana, long Summa, decimal Litr);

public sealed record OperatorQisqaDto(int OperatorId, string Ism, long BugungiSumma, decimal BugungiLitr, int SotuvSoni, bool SmenaOchiqmi);

public sealed record BoshqaruvBugunDto(
    BoshqaruvKpiDto Kpi,
    TolovUlushiDto[] TolovUlushlari,
    YoqilgiUlushiDto[] YoqilgiUlushlari,
    KunlikDto[] OxirgiKunlar,
    OperatorQisqaDto[] Operatorlar,
    SotuvDto[] OxirgiSotuvlar);
