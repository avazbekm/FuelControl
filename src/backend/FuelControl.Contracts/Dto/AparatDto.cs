namespace FuelControl.Contracts.Dto;

/// <summary>
/// TotalLitr — pultdagi "Total, L" (oxirgi yopilgan smena holatida). BakQoldiq — aparatning o'z baki, litrda:
/// kirimlar − sotilgan (smena yopilganda ayriladi). OxirgiKirim* — oxirgi bakka kirim (yo'q bo'lsa null).
/// KrugHajmi — pult 0 ga qaytadigan hajm, litrda (masalan 10 000: pult 9 999.99 dan keyin 0 dan boshlaydi va "krug" bittaga oshadi); null —
/// krug yo'q. Dastur ichida va API'da ko'rsatkich doim UMUMIY litr (TotalLitr, segmentlarning Boshi/Oxiri, AparatKorsatkichDto.Qiymat); krug
/// faqat klientda ko'rsatish va kiritish qoidasi: krug = floor(T / H) + 1, pult = T − (krug − 1) × H.
/// </summary>
public sealed record AparatDto(int Id, int Raqam, int YoqilgiTuriId, string YoqilgiNomi, decimal TotalLitr,
    decimal BakQoldiq, DateTime? OxirgiKirimVaqti, decimal? OxirgiKirimLitr, decimal? KrugHajmi);

/// <summary>KrugHajmi: null yoki 0 — krug yo'q; &gt; 0 — pult shu hajmda 0 ga qaytadi; manfiy — 400.</summary>
public sealed record AparatYaratishDto(int Raqam, int YoqilgiTuriId, decimal BoshlangichTotalLitr, decimal BoshlangichBakQoldiq, decimal? KrugHajmi = null);

/// <summary>
/// TotalLitr yoki BakQoldiq berilsa va farq qilsa — qo'lda tuzatiladi: Sabab majburiy, auditga yoziladi.
/// KrugHajmi: null — O'ZGARMAYDI (eski klient maydonni yubormasa sozlama o'chib qolmasin); 0 — krug o'chiriladi; &gt; 0 — krug hajmi o'rnatiladi;
/// manfiy — 400. Faqat ko'rinish sozlamasi bo'lgani uchun ochiq smenada ham o'zgartiriladi (409 qoidasi faqat TotalLitr va yoqilg'iga tegishli).
/// </summary>
public sealed record AparatTahrirlashDto(int Raqam, int YoqilgiTuriId, decimal? TotalLitr = null, decimal? BakQoldiq = null, string? Sabab = null,
    decimal? KrugHajmi = null);

/// <summary>Zavoddan kelgan yoqilg'i, litrda. Vaqt berilmasa — hozir. Hujjat — raqam yoki izoh.</summary>
public sealed record BakKirimYaratishDto(decimal Litr, DateTime? Vaqt, string? Hujjat);

public sealed record BakKirimDto(int Id, int AparatId, int AparatRaqam, decimal Litr, decimal QoldiqOldin, decimal QoldiqKeyin,
    DateTime Vaqt, string? Hujjat, string KimYozdi);
