namespace FuelControl.Contracts.Dto;

/// <summary>
/// MuddatgachaKun: Muddat - bugun (Toshkent), manfiy = o'tgan. Qoldiq = Summa - Qaytgan.
/// MuallifId - yozgan foydalanuvchi: klient "o'z yozuvi"ni ism bo'yicha emas, shu bo'yicha tekshiradi.
/// Telefon doim "+998 XX XXX XX XX" ko'rinishida (telefon kiritilmagan bo'lsa - bo'sh satr).
/// </summary>
public sealed record NasiyaDto(int Id, int SmenaId, string OperatorIsmi, string MijozIsmi, string Telefon, string MashinaRaqami,
    long Summa, long Qaytgan, long Qoldiq, DateOnly Muddat, NasiyaHolati Holati, int MuddatgachaKun,
    DateTime Yozildi, DateTime? Yopildi, string? Izoh, int MuallifId);

/// <summary>
/// Telefon istalgan ko'rinishda yuborilishi mumkin ("90 123 45 67", "998901234567", "+998-90-123-45-67"): server raqam bo'lmagan belgilarni
/// tashlaydi, boshidagi 998 ni tushiradi va aynan 9 raqam qolishini talab qiladi (aks holda 400), so'ng "+998 XX XXX XX XX" ko'rinishida saqlaydi.
/// Telefon bo'sh bo'lishi mumkin - agar mashina raqami berilgan bo'lsa (ikkalasidan kamida bittasi shart).
/// </summary>
public sealed record NasiyaYaratishDto(string MijozIsmi, string Telefon, string MashinaRaqami, long Summa, DateOnly Muddat, string? Izoh);

/// <summary>Summa &lt;= qoldiq. SmenaHisobiga = true: ochiq smena majburiy, qaytish smena hisobiga kirim bo'ladi; false: smena hisobiga ta'sir qilmaydi (boshliq).</summary>
public sealed record NasiyaQaytishiYaratishDto(long Summa, TolovTuri Usul, bool SmenaHisobiga, string? Izoh);

/// <summary>SmenaId - qaytish yozilgan smena (smena hisobiga yozilmagan bo'lsa null).</summary>
public sealed record NasiyaQaytishiDto(int Id, int NasiyaId, string MijozIsmi, int? SmenaId, long Summa, TolovTuri Usul,
    DateTime Vaqt, string KimYozdi, string? Izoh, int MuallifId);

public sealed record NasiyaTafsilotDto(NasiyaDto Nasiya, NasiyaQaytishiDto[] Qaytishlar);

/// <summary>FaolQarz/FaolSoni - qoldig'i bor barcha nasiyalar (muddati o'tganlar ham); Oy* - joriy (Toshkent) oy.</summary>
public sealed record NasiyalarXulosaDto(long FaolQarz, int FaolSoni, long MuddatiOtgan, int MuddatiOtganSoni,
    long OyBerilgan, int OyBerilganSoni, long OyQaytgan, int OyQaytganSoni);

public sealed record NasiyalarDto(NasiyalarXulosaDto Xulosa, NasiyaDto[] Royxat);

/// <summary>
/// GET /nasiyalar/mijozlar?q= - mavjud mijoz taklifi (nasiya yozuvlaridan yig'iladi: telefon bo'lsa telefon, bo'lmasa ism + mashina raqami bo'yicha).
/// Telefon "+998 XX XXX XX XX". NasiyaSoni - mijozning hamma nasiyalari, FaolQarz - ularning umumiy qoldig'i, OxirgiNasiya - eng oxirgi nasiya
/// yozilgan Toshkent sanasi. MijozIsmi/MashinaRaqami - so'rovga eng yaxshi mos (keyin eng yangi) yozuvdan.
/// </summary>
public sealed record MijozTaklifDto(string MijozIsmi, string Telefon, string MashinaRaqami, int NasiyaSoni, long FaolQarz, DateOnly OxirgiNasiya);
