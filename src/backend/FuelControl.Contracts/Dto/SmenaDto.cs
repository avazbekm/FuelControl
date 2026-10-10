namespace FuelControl.Contracts.Dto;

/// <summary>
/// Smena = bir sutka, butun shoxobchada bir vaqtda faqat bitta ochiq smena. Pul — so'm (long), litr — decimal(18,2).
/// Ochiq smenada JamiLitr/Savdo/Plastik/DepozitFarqi/Kutilgan/Farq = 0; NasiyaJami/QaytganNasiya/XarajatJami — joriy yig'indilar.
/// Yopilgan smenada: Plastik = YopishTerminal - OchishTerminal, DepozitFarqi = YopishDepozit - OchishDepozit (manfiy bo'lishi mumkin),
/// Kutilgan = OchishQaytim + Savdo + QaytganNasiya - Plastik - DepozitFarqi - NasiyaJami - XarajatJami, Farq = SanalganNaqd - Kutilgan
/// (manfiy - kamomat, musbat - ortiqcha).
/// PlastikSummalari - yopishda kiritilgan plastik qismlari (terminal, kassa aparati, nollash cheki...): yig'indisi YopishTerminal'ga teng;
/// ochiq smenada va qismlarsiz yopilgan (eski) smenalarda bo'sh massiv - u holda faqat YopishTerminal ko'rsatiladi.
/// </summary>
public sealed record SmenaDto(
    int Id, int OperatorId, string OperatorIsmi, DateTime Boshlandi, DateTime? Tugadi,
    long OchishQaytim, long OchishTerminal, long OchishDepozit,
    long? YopishTerminal, long? YopishDepozit, long? SanalganNaqd,
    decimal JamiLitr, long Savdo, long Plastik, long DepozitFarqi,
    long NasiyaJami, long QaytganNasiya, long XarajatJami,
    long Kutilgan, long Farq, string? Izoh, long[] PlastikSummalari);

/// <summary>
/// Smenaning bitta aparat segmenti. Odatda aparatga bitta qator; ochiq smenada narx o'zgargan bo'lsa ikkita (NarxOzgarishida = true:
/// birinchi segment eski narxda, narx o'zgargan paytdagi ko'rsatkichgacha). Litr = Oxiri - Boshi (2 xona), Summa = round(Litr x Narx).
/// </summary>
public sealed record SmenaKorsatkichDto(int AparatId, int AparatRaqam, string YoqilgiNomi,
    decimal Boshi, decimal Oxiri, long Narx, decimal Litr, long Summa, bool NarxOzgarishida);

/// <summary>
/// Smena tafsiloti. Yopilgan smenada Korsatkichlar - barcha segmentlar (Tartib bo'yicha). Ochiq smenada faqat narx o'zgarishida
/// qayd etilgan segmentlar (NarxOzgarishida = true); narx o'zgarmagan bo'lsa bo'sh - savdo yopilganda pult ko'rsatkichlaridan hisoblanadi.
/// </summary>
public sealed record SmenaTafsilotDto(SmenaDto Smena, SmenaKorsatkichDto[] Korsatkichlar,
    NasiyaDto[] Nasiyalar, NasiyaQaytishiDto[] Qaytishlar, XarajatDto[] Xarajatlar);

/// <summary>
/// GET /smenalar/oxirgi/topshirish - keyingi operator uchun oldingi (oxirgi yopilgan) smenaning topshirish ma'lumoti. Pul natijasi YO'Q
/// (savdo, plastik, terminal, kamomat... ko'rsatilmaydi): to'liq natija /smenalar/oxirgi da, faqat boshliq (Smenalar ruxsati) va shu smenaning
/// operatoriga. OperatorId - klient "bu mening smenammi"ni ism bo'yicha emas, shu Id bo'yicha hal qiladi (o'ziniki bo'lsa to'liq natija
/// /smenalar/oxirgi dan olinadi, 403 chiqmaydi). Tugadi - UTC. YopishDepozit - yopilgandagi depozit karta qoldig'i (faqat ma'lumot uchun).
/// </summary>
public sealed record SmenaTopshirishDto(int Id, int OperatorId, string OperatorIsmi, DateTime Tugadi, long? YopishDepozit);

/// <summary>Boshqaruv grafigi uchun: Sana - smena ochilgan Toshkent sanasi.</summary>
public sealed record SmenaQisqaDto(int Id, DateOnly Sana, string OperatorIsmi, long Savdo, decimal Litr, long Farq);

/// <summary>Smenani ochish: uchala qiymat operator tomonidan qo'lda kiritiladi (oldingi smenadan avtomatik olinmaydi).</summary>
public sealed record SmenaOchishDto(long Qaytim, long Terminal, long Depozit);

public sealed record AparatKorsatkichDto(int AparatId, decimal Qiymat);

/// <summary>
/// Smenani yopish: har faol aparat uchun pult ko'rsatkichi majburiy va oldingisidan kichik bo'lmasligi shart; Terminal - plastikning jami
/// summasi (tungi nollash cheki + nollashdan keyingi savdo + kassa aparati...); Depozit - karta qoldig'i; SanalganNaqd - kassada sanalgan naqd.
/// Pul maydonlari >= 0. Smena plastigi = Terminal - OchishTerminal (formula o'zgarmagan).
/// PlastikSummalari (ixtiyoriy): plastik qismlari. Berilsa, har biri >= 0, ko'pi bilan 20 ta va yig'indisi Terminal'ga teng bo'lishi shart
/// (aks holda 400); smenada saqlanadi va SmenaDto.PlastikSummalari bo'lib qaytadi. Berilmasa (eski klientlar) - eski xatti-harakat: faqat Terminal.
/// </summary>
public sealed record SmenaYopishDto(AparatKorsatkichDto[] Korsatkichlar, long Terminal, long Depozit, long SanalganNaqd, string? Izoh,
    long[]? PlastikSummalari = null);

/// <summary>Faqat oxirgi yopilgan smena uchun (KorsatkichTuzatish ruxsati), Sabab majburiy.</summary>
public sealed record KorsatkichTuzatishDto(int AparatId, decimal Qiymat, string Sabab);
