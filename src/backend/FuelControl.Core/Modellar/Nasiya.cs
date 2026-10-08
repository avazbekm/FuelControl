using FuelControl.Contracts;

namespace FuelControl.Core.Modellar;

/// <summary>Nasiya (qarzga yoqilg'i). Qaytgan — qaytishlar yig'indisi (denormalizatsiya, NasiyaXizmati yangilaydi).</summary>
public sealed class Nasiya
{
    public int Id { get; set; }

    /// <summary>Yozilgan smena.</summary>
    public int SmenaId { get; set; }
    public int OperatorId { get; set; }
    public string KimYozdi { get; set; } = "";
    public string MijozIsmi { get; set; } = "";
    public string Telefon { get; set; } = "";
    public string MashinaRaqami { get; set; } = "";
    public long Summa { get; set; }
    public long Qaytgan { get; set; }
    public DateOnly Muddat { get; set; }
    public DateTime Yozildi { get; set; }

    /// <summary>Qoldiq 0 bo'lgan vaqt (to'liq qaytgan); qoldiq qayta paydo bo'lsa null.</summary>
    public DateTime? Yopildi { get; set; }
    public string? Izoh { get; set; }

    public long Qoldiq => Summa - Qaytgan;
}

public sealed class NasiyaQaytishi
{
    public int Id { get; set; }
    public int NasiyaId { get; set; }

    /// <summary>Qaytish smena hisobiga yozilgan bo'lsa — o'sha (ochiq) smena; boshliq smenadan tashqari yozgan bo'lsa null.</summary>
    public int? SmenaId { get; set; }
    public long Summa { get; set; }
    public TolovTuri Usul { get; set; }
    public DateTime Vaqt { get; set; }
    public int OperatorId { get; set; }
    public string KimYozdi { get; set; } = "";
    public string? Izoh { get; set; }
}

/// <summary>Smena davomidagi xarajat. Manba Kassa yoki Depozit karta.</summary>
public sealed class Xarajat
{
    public int Id { get; set; }
    public int SmenaId { get; set; }
    public long Summa { get; set; }
    public string Sabab { get; set; } = "";
    public XarajatManbai Manba { get; set; }
    public DateTime Vaqt { get; set; }
    public int OperatorId { get; set; }
    public string KimYozdi { get; set; } = "";
}
