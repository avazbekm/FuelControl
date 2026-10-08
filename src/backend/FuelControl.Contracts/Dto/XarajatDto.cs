namespace FuelControl.Contracts.Dto;

/// <summary>Manba Kassa yoki Depozit karta. Smena hisobi uchun ikkalasi ham xarajat; Depozit manbasi kassaga ta'sir qilmaydi.</summary>
public sealed record XarajatDto(int Id, int SmenaId, long Summa, string Sabab, XarajatManbai Manba, DateTime Vaqt, string KimYozdi, int MuallifId);

public sealed record XarajatYaratishDto(long Summa, string Sabab, XarajatManbai Manba);
