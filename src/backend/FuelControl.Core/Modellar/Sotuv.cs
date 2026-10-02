using FuelControl.Contracts;

namespace FuelControl.Core.Modellar;

public sealed class SotuvTolovi
{
    public int Id { get; set; }
    public int SotuvId { get; set; }
    public TolovTuri Turi { get; set; }
    public long Summa { get; set; }
}

public sealed class Sotuv
{
    public int Id { get; set; }
    public int SmenaId { get; set; }
    public int OperatorId { get; set; }
    public int AparatId { get; set; }

    /// <summary>Sotuv paytidagi narx — keyinchalik yoqilg'i narxi o'zgarsa ham o'zgarmaydi.</summary>
    public long Narx { get; set; }
    public decimal Litr { get; set; }
    public long Summa { get; set; }
    public DateTime Vaqt { get; set; }
    public List<SotuvTolovi> Tolovlar { get; set; } = new();
    public SotuvHolati Holati { get; set; } = SotuvHolati.Faol;
    public string? BekorSababi { get; set; }
    public string? BekorQilgan { get; set; }

    /// <summary>Takror yuborilgan so'rovni ikkinchi marta yozmaslik uchun.</summary>
    public Guid IdempotencyKey { get; set; }

    public bool Faolmi => Holati == SotuvHolati.Faol;
}
