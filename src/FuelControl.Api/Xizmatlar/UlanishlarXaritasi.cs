using System.Collections.Concurrent;
using FuelControl.Api.Auth;
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
