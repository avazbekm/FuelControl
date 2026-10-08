namespace FuelControl.Core.Modellar;

/// <summary>Bakka kirim (zavoddan kelgan yoqilg'i), litrda. QoldiqOldin/Keyin — yozilgan paytdagi bak qoldig'i.</summary>
public sealed class BakKirim
{
    public int Id { get; set; }
    public int AparatId { get; set; }
    public decimal Litr { get; set; }
    public decimal QoldiqOldin { get; set; }
    public decimal QoldiqKeyin { get; set; }

    /// <summary>Kelgan vaqt (foydalanuvchi kiritadi, bo'sh bo'lsa — yozilgan vaqt).</summary>
    public DateTime Vaqt { get; set; }
    public string? Hujjat { get; set; }
    public string KimYozdi { get; set; } = "";
}

/// <summary>Bak qoldig'ini Sozlamalarda qo'lda tuzatish (o'lchov bo'yicha). Hisobotda bak qoldig'ini tarixiy davrga qaytarish uchun saqlanadi.</summary>
public sealed class BakTuzatishi
{
    public int Id { get; set; }
    public int AparatId { get; set; }
    public DateTime Vaqt { get; set; }
    public decimal Oldin { get; set; }
    public decimal Keyin { get; set; }
    public string Sabab { get; set; } = "";
    public string KimYozdi { get; set; } = "";
}
