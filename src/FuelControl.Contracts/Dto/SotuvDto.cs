namespace FuelControl.Contracts.Dto;

public sealed record TolovDto(TolovTuri Turi, long Summa);

public sealed record SotuvDto(
    int Id,
    int SmenaId,
    int OperatorId,
    string OperatorIsmi,
    int AparatId,
    int AparatRaqami,
    string YoqilgiNomi,
    long Narx,
    decimal Litr,
    long Summa,
    DateTime Vaqt,
    TolovDto[] Tolovlar,
    SotuvHolati Holati,
    string? BekorSababi,
    string? BekorQilgan);

/// <summary>
/// Litr yoki Summa'dan faqat bittasi kiritiladi — ikkinchisi joriy narxdan hisoblanadi.
/// To'lov: bitta to'lov turi bo'lsa — TolovTuri (summani server o'zi qo'yadi; offline navbatdan kelganda narx o'zgargan
/// bo'lsa ham mos keladi); aralash bo'lsa — Tolovlar[] (yig'indi = summa). Faqat bittasi beriladi.
/// </summary>
public sealed record SotuvYaratishDto(int AparatId, decimal? Litr, long? Summa, TolovDto[]? Tolovlar, Guid IdempotencyKey, TolovTuri? TolovTuri = null);

public sealed record SotuvTahrirlashDto(int AparatId, long Summa, TolovDto[] Tolovlar, string Sabab);

public sealed record SotuvBekorQilishDto(string Sabab);
