using FuelControl.Contracts;

namespace FuelControl.Core.Modellar;

/// <summary>Operator hisob-varaqasi harakati (maosh/avans/kamomat/ortiqcha/to'lov).</summary>
public sealed class HisobHarakati
{
    public int Id { get; set; }
    public int OperatorId { get; set; }
    public DateTime Sana { get; set; }
    public HarakatTuri Turi { get; set; }

    /// <summary>Operator foydasiga musbat (Maosh, Ortiqcha), hisobidan manfiy (Avans, Kamomat, Tolov).</summary>
    public long Summa { get; set; }
    public string Izoh { get; set; } = "";
    public string KimYozdi { get; set; } = "";

    /// <summary>Faqat Turi = Maosh uchun: "yyyy-MM". (OperatorId, MaoshOyi) noyob — bir oyga ikki marta maosh yozilmaydi.</summary>
    public string? MaoshOyi { get; set; }
}

public sealed class AuditYozuvi
{
    public int Id { get; set; }
    public DateTime Vaqt { get; set; }
    public string Kim { get; set; } = "";
    public string Amal { get; set; } = "";
    public string Tafsilot { get; set; } = "";
}
