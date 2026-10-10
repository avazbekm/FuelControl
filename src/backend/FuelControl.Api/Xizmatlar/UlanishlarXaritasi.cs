using System.Collections.Concurrent;
using FuelControl.Api.Auth;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.SignalR;

namespace FuelControl.Api.Xizmatlar;

/// <summary>
/// Foydalanuvchi → uning SignalR ulanishlari. Guruh (kuzatuvchi/egasi) ulanish paytida belgilanadi, shuning uchun
/// foydalanuvchining ruxsati, roli yoki faolligi o'zgarsa — ulanishlariga "QaytaUlan" yuborib uzamiz; mijoz qayta
/// ulanganda yangi ruxsatlar bilan kiradi (nofaol bo'lsa — kira olmaydi).
/// </summary>
public sealed class UlanishlarXaritasi(IHubContext<SotuvHub> hub, ILogger<UlanishlarXaritasi> log)
{
    public const string QaytaUlan = "QaytaUlan";

    private readonly ConcurrentDictionary<string, (int FoydalanuvchiId, HubCallerContext Kontekst)> _ulanishlar = new();

    public void Qosh(HubCallerContext c) => _ulanishlar[c.ConnectionId] = (c.User!.FoydalanuvchiId(), c);

    public void Olib(string connectionId) => _ulanishlar.TryRemove(connectionId, out _);

    public int Soni(int foydalanuvchiId) => _ulanishlar.Values.Count(x => x.FoydalanuvchiId == foydalanuvchiId);

    /// <summary>
    /// Yopilgan smena hodisasi (yopish, ko'rsatkich tuzatish) qabul qiluvchilari: to'liq natija - "Smenalar" ruxsatlilar va smena egasi;
    /// kuzatuvchilarning qolgani (masalan, keyingi operator) uchun pul natijalari yashirilgan nusxa (docs 8.10). Ruxsat ulanish paytida
    /// belgilanadi, o'zgarsa ulanish uziladi (<see cref="Uz"/>), shuning uchun xarita ulanish guruhlari bilan bir xil ma'lumotga tayanadi.
    /// </summary>
    public (List<string> Toliq, List<string> Yashirin) SmenaQabulqiluvchilari(int egasiId)
    {
        List<string> toliq = [], yashirin = [];
        foreach (var (id, (foydalanuvchiId, kontekst)) in _ulanishlar)
        {
            var u = kontekst.User!;
            if (foydalanuvchiId == egasiId || u.Bor(Ruxsat.Smenalar)) toliq.Add(id);
            else if (SotuvHub.Kuzatuvchimi(u)) yashirin.Add(id);
        }
        return (toliq, yashirin);
    }

    /// <summary>
    /// Yopilgan smenaning SmenaOzgardi hodisasi: to'liq DTO faqat "Smenalar" ruxsatlilarga va egasiga; qolgan kuzatuvchilarga pul maydonlari
    /// nollangan nusxa - keyingi operator oldingi operatorning natijasini ulanish darajasida ham olmaydi.
    /// </summary>
    public async Task YopilganSmena(SmenaDto toliq)
    {
        var (toliqIdlar, yashirinIdlar) = SmenaQabulqiluvchilari(toliq.OperatorId);
        if (toliqIdlar.Count > 0) await hub.Clients.Clients(toliqIdlar).SendAsync(Xabarlar.SmenaOzgardi, toliq);
        if (yashirinIdlar.Count > 0) await hub.Clients.Clients(yashirinIdlar).SendAsync(Xabarlar.SmenaOzgardi, toliq.PulSiz());
    }

    public async Task Uz(int foydalanuvchiId)
    {
        var royxat = _ulanishlar.Where(x => x.Value.FoydalanuvchiId == foydalanuvchiId).ToList();
        if (royxat.Count == 0) return;
        try { await hub.Clients.Clients(royxat.Select(x => x.Key).ToList()).SendAsync(QaytaUlan); }
        catch (Exception e) { log.LogWarning(e, "QaytaUlan yuborilmadi"); }
        foreach (var (id, (_, kontekst)) in royxat)
        {
            kontekst.Abort();
            _ulanishlar.TryRemove(id, out _);
        }
    }
}
