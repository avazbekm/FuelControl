namespace FuelControl.Contracts.Dto;

public sealed record AuditYozuviDto(int Id, DateTime Vaqt, string Kim, string Amal, string Tafsilot);

/// <summary>Klientda qilingan eksport (Excel) — auditga yozish uchun. Turi: masalan "Hisobot", "Hisob-varaqa".</summary>
public sealed record AuditEksportDto(string Turi, string Tafsilot);
