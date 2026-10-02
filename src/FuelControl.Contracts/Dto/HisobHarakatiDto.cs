namespace FuelControl.Contracts.Dto;

public sealed record HisobHarakatiDto(int Id, int OperatorId, DateTime Sana, HarakatTuri Turi, long Summa, string Izoh, string KimYozdi);

/// <summary>Faqat Avans yoki Tolov turlari klientdan yaratiladi; Maosh/Kamomat/Ortiqcha tizim tomonidan yoziladi.</summary>
public sealed record HarakatYaratishDto(HarakatTuri Turi, long Summa, string Izoh);

/// <summary>OyJami — so'ralgan oy harakatlari yig'indisi; OySavdo/OySmenalar — joriy (Toshkent) oy bo'yicha.</summary>
public sealed record OperatorHisobDto(
    int OperatorId,
    string OperatorIsmi,
    long OylikMaosh,
    long OyJami,
    long Qoldiq,
    long OySavdo,
    int OySmenalar,
    HisobHarakatiDto[] Harakatlar);
