namespace FuelControl.Contracts.Dto;

/// <summary>Tur: "smena" | "nasiya" | "xarajat" | "bak" | "tuzatish" | "hisob" | "sozlama" | "kirish".</summary>
public sealed record AuditYozuviDto(int Id, DateTime Vaqt, string Kim, string Amal, string Tafsilot, string Tur);

/// <summary>Klientda qilingan eksport (Excel) - auditga yozish uchun. Turi: masalan "Hisobot", "Hisob-varaqa".</summary>
public sealed record AuditEksportDto(string Turi, string Tafsilot);
