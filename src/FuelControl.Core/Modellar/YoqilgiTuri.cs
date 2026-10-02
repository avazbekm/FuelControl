namespace FuelControl.Core.Modellar;

public sealed class YoqilgiTuri
{
    public int Id { get; set; }
    public string Nomi { get; set; } = "";
    public long Narx { get; set; }
    public string Rang { get; set; } = "#2F6BFF";
}

/// <summary>Narx o'zgarishi tarixi — o'zgarmas voqea yozuvi, shuning uchun "Kim" ism sifatida saqlanadi.</summary>
public sealed class NarxTarixi
{
    public int Id { get; set; }
    public int YoqilgiTuriId { get; set; }
    public string YoqilgiNomi { get; set; } = "";
    public DateTime Vaqt { get; set; }
    public long EskiNarx { get; set; }
    public long YangiNarx { get; set; }
    public string Kim { get; set; } = "";
}
