namespace FuelControl.Contracts.Dto;

/// <summary>
/// MuddatgachaKun: Muddat - bugun (Toshkent), manfiy = o'tgan. Qoldiq = Summa - Qaytgan.
/// MuallifId - yozgan foydalanuvchi: klient "o'z yozuvi"ni ism bo'yicha emas, shu bo'yicha tekshiradi.
/// Telefon doim "+998 XX XXX XX XX" ko'rinishida (telefon kiritilmagan bo'lsa - bo'sh satr).
/// MuallifId = Nasiya.OperatorId (GET /nasiyalar?operatorId= shu maydon bo'yicha filtrlaydi).
/// EskiQarz = true - dasturdan oldingi qarzning to'lovi (POST /nasiyalar/eski-qarz): u hech qachon "berilgan nasiya" emas (smena NasiyaJami,
/// "oy berilgan" va operatorning "berilgan"iga kirmaydi), darhol yopilgan (Qaytgan = Summa), qaytishi bilan birga yaratiladi va o'chadi.
/// </summary>
public sealed record NasiyaDto(int Id, int SmenaId, string OperatorIsmi, string MijozIsmi, string Telefon, string MashinaRaqami,
    long Summa, long Qaytgan, long Qoldiq, DateOnly Muddat, NasiyaHolati Holati, int MuddatgachaKun,
    DateTime Yozildi, DateTime? Yopildi, string? Izoh, int MuallifId, bool EskiQarz);

/// <summary>
/// Telefon istalgan ko'rinishda yuborilishi mumkin ("90 123 45 67", "998901234567", "+998-90-123-45-67"): server raqam bo'lmagan belgilarni
/// tashlaydi, boshidagi 998 ni tushiradi va aynan 9 raqam qolishini talab qiladi (aks holda 400), so'ng "+998 XX XXX XX XX" ko'rinishida saqlaydi.
/// Telefon bo'sh bo'lishi mumkin - agar mashina raqami berilgan bo'lsa (ikkalasidan kamida bittasi shart).
/// </summary>
public sealed record NasiyaYaratishDto(string MijozIsmi, string Telefon, string MashinaRaqami, long Summa, DateOnly Muddat, string? Izoh);

/// <summary>Summa &lt;= qoldiq. SmenaHisobiga = true: ochiq smena majburiy, qaytish smena hisobiga kirim bo'ladi; false: smena hisobiga ta'sir qilmaydi (boshliq).</summary>
public sealed record NasiyaQaytishiYaratishDto(long Summa, TolovTuri Usul, bool SmenaHisobiga, string? Izoh);

/// <summary>
/// SmenaId - qaytish yozilgan smena (smena hisobiga yozilmagan bo'lsa null). EskiQarz = true - bu dasturdan oldingi qarzning to'lovi
/// (uning nasiyasi NasiyaId bo'yicha topiladi, lekin smena tafsilotining Nasiyalar ro'yxatida chiqmaydi).
/// </summary>
public sealed record NasiyaQaytishiDto(int Id, int NasiyaId, string MijozIsmi, int? SmenaId, long Summa, TolovTuri Usul,
    DateTime Vaqt, string KimYozdi, string? Izoh, int MuallifId, bool EskiQarz);

public sealed record NasiyaTafsilotDto(NasiyaDto Nasiya, NasiyaQaytishiDto[] Qaytishlar);

/// <summary>
/// FaolQarz/FaolSoni - qoldig'i bor barcha nasiyalar (muddati o'tganlar ham); Oy* - joriy (Toshkent) oy. OyBerilgan/OyBerilganSoni - faqat
/// oddiy nasiyalar (eski qarz to'lovi "berilgan" emas); OyQaytgan/OyQaytganSoni - hamma qaytishlar (eski qarz to'lovlari ham).
/// </summary>
public sealed record NasiyalarXulosaDto(long FaolQarz, int FaolSoni, long MuddatiOtgan, int MuddatiOtganSoni,
    long OyBerilgan, int OyBerilganSoni, long OyQaytgan, int OyQaytganSoni);

/// <summary>
/// POST /nasiyalar/eski-qarz - dasturdan oldingi qarzdorning olib kelgan puli. Faqat to'langan summa yoziladi ("qolgan qarz" yo'q).
/// Ism majburiy; telefon va mashina raqami ixtiyoriy (bo'sh satr/null mumkin), lekin berilsa nasiyadagi qoidalar amal qiladi: telefon
/// 9 raqamga keltiriladi ("+998 XX XXX XX XX", noto'g'ri bo'lsa 400). Summa &gt; 0. Usul - Naqd, Plastik yoki Depozit (smena formulasidagi
/// kabi: plastik terminalga, depozit karta qoldig'iga tushadi). Ochiq smena majburiy (yo'q bo'lsa 409, nasiya yozishdagidek).
/// </summary>
public sealed record EskiQarzYaratishDto(string MijozIsmi, string Telefon, string MashinaRaqami, long Summa, TolovTuri Usul, string? Izoh);

/// <summary>
/// Bitta operator (Nasiya.OperatorId = NasiyaDto.MuallifId) bo'yicha yig'indilar - butun vaqt, holat va q filtriga bog'liq emas.
/// Soni/Berilgan/Qaytgan/Qoldiq/FaolSoni - faqat oddiy (EskiQarz = false) nasiyalar: Berilgan - yozilgan summalar, Qaytgan - ularga qaytgan,
/// Qoldiq = Berilgan - Qaytgan, FaolSoni - qoldig'i bor nasiyalar soni. EskiQarzQabul - shu operator qabul qilgan eski qarz to'lovlari
/// yig'indisi (ular yuqoridagilarga kirmaydi). OperatorIsmi - foydalanuvchining hozirgi ismi (topilmasa nasiyalardagi oxirgi ism).
/// </summary>
public sealed record NasiyaOperatorXulosaDto(int OperatorId, string OperatorIsmi, int Soni, long Berilgan, long Qaytgan, long Qoldiq,
    int FaolSoni, long EskiQarzQabul);

/// <summary>Operator - faqat GET /nasiyalar?operatorId=N berilganda to'ldiriladi (aks holda null). Xulosa filtrga bog'liq emas.</summary>
public sealed record NasiyalarDto(NasiyalarXulosaDto Xulosa, NasiyaDto[] Royxat, NasiyaOperatorXulosaDto? Operator);

/// <summary>
/// GET /nasiyalar/mijozlar?q= - mavjud mijoz taklifi (nasiya yozuvlaridan yig'iladi: telefon bo'lsa telefon, bo'lmasa ism + mashina raqami bo'yicha).
/// Telefon "+998 XX XXX XX XX". NasiyaSoni - mijozning hamma nasiyalari, FaolQarz - ularning umumiy qoldig'i, OxirgiNasiya - eng oxirgi nasiya
/// yozilgan Toshkent sanasi. MijozIsmi/MashinaRaqami - so'rovga eng yaxshi mos (keyin eng yangi) yozuvdan.
/// </summary>
public sealed record MijozTaklifDto(string MijozIsmi, string Telefon, string MashinaRaqami, int NasiyaSoni, long FaolQarz, DateOnly OxirgiNasiya);
