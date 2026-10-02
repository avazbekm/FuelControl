namespace FuelControl.Core.Modellar;

public sealed class Smena
{
    public int Id { get; set; }
    public int OperatorId { get; set; }
    public DateTime Boshlandi { get; set; }
    public DateTime? Tugadi { get; set; }

    // Hisoblangan (faol sotuvlardan) — SmenaHisoblagich.QaytaHisobla orqali yangilanadi.
    public long KutilganNaqd { get; set; }
    public long KutilganPlastik { get; set; }
    public long KutilganClick { get; set; }
    public decimal JamiLitr { get; set; }
    public int SotuvSoni { get; set; }

    // Yopishda operator topshirgan — har to'lov turi alohida.
    public long? TopshirilganNaqd { get; set; }
    public long? TopshirilganPlastik { get; set; }
    public long? TopshirilganClick { get; set; }

    public string? Izoh { get; set; }

    public bool Ochiqmi => Tugadi is null;
}
