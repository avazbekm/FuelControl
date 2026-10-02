namespace FuelControl.Core.Modellar;

public sealed class Aparat
{
    public int Id { get; set; }
    public int Raqam { get; set; }
    public int YoqilgiTuriId { get; set; }

    /// <summary>Totalizator — pultdagi "Total L": boshlang'ich qiymat + barcha faol sotuvlar yig'indisi.</summary>
    public decimal TotalLitr { get; set; }
}
