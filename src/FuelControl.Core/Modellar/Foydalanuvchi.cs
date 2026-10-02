using FuelControl.Contracts;

namespace FuelControl.Core.Modellar;

public sealed class Foydalanuvchi
{
    public int Id { get; set; }
    public string ToliqIsm { get; set; } = "";
    public string Login { get; set; } = "";
    public Rol Rol { get; set; }
    public bool Faol { get; set; } = true;
    public long OylikMaosh { get; set; }

    /// <summary>PBKDF2 xeshi: "iteratsiya.tuzBase64.xeshBase64".</summary>
    public string ParolXeshi { get; set; } = "";

    /// <summary>Har foydalanuvchiga alohida ruxsatlar; rol faqat boshlang'ich to'plamni beradi.</summary>
    public List<Ruxsat> Ruxsatlar { get; set; } = new();

    public int XatoUrinishlar { get; set; }
    public DateTime? BlokGacha { get; set; }
}
