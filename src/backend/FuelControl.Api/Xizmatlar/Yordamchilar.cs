using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

/// <summary>Sotuv/smena xabarlari hammaga emas: faqat ko'rish ruxsati borlar ("kuzatuvchi") va egasiga ("f-{id}").</summary>
public sealed class SotuvHub(UlanishlarXaritasi ulanishlar) : Hub
{
    public const string Kuzatuvchilar = "kuzatuvchi";
    public static string Egasi(int foydalanuvchiId) => $"f-{foydalanuvchiId}";

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        ulanishlar.Olib(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public override async Task OnConnectedAsync()
    {
        ulanishlar.Qosh(Context);
        var u = Context.User!;
        await Groups.AddToGroupAsync(Context.ConnectionId, Egasi(u.FoydalanuvchiId()));
        // Smena, nasiya, xarajat va aparat o'zgarishlarini ko'ra oladiganlar (operativ ish va hisobot ruxsatlari).
        if (u.Bor(Ruxsat.Savdo) || u.Bor(Ruxsat.Nasiyalar) || u.Bor(Ruxsat.Smenalar) || u.Bor(Ruxsat.Hisobotlar) || u.Bor(Ruxsat.Boshqaruv))
            await Groups.AddToGroupAsync(Context.ConnectionId, Kuzatuvchilar);
        await base.OnConnectedAsync();
    }
}

public static class Xabarlar
{
    // Hodisalar o'zgarish signali: klient tegishli ma'lumotni qayta yuklasin (smena: /smenalar/joriy). Yuk — o'zgargan yozuv DTO'si.
    public const string SmenaOzgardi = "SmenaOzgardi";
    public const string NasiyaOzgardi = "NasiyaOzgardi";
    public const string XarajatOzgardi = "XarajatOzgardi";
    public const string AparatOzgardi = "AparatOzgardi";
    public const string NarxOzgardi = "NarxOzgardi";

    /// <summary>Boshqa o'zgarishlar: parametr — bo'lim nomi (Bolimlar.*), klient o'sha ro'yxatni qayta yuklaydi.</summary>
    public const string Ozgardi = "Ozgardi";
}

public static class HubKengaytmasi
{
    public static Task Bildir(this IHubContext<SotuvHub> hub, string bolim) => hub.Clients.All.SendAsync(Xabarlar.Ozgardi, bolim);

    /// <summary>Smena, nasiya, xarajat, aparat o'zgarishi: faqat ko'rish ruxsati borlarga (kuzatuvchilar). Yuk — o'zgargan yozuv DTO'si; klient qayta yuklaydi.</summary>
    public static Task Kuzatuvchilarga(this IHubContext<SotuvHub> hub, string xabar, object dto) =>
        hub.Clients.Group(SotuvHub.Kuzatuvchilar).SendAsync(xabar, dto);
}

public static class Bolimlar
{
    public const string Foydalanuvchilar = "Foydalanuvchilar";
    public const string Yoqilgilar = "Yoqilgilar";
    public const string Aparatlar = "Aparatlar";
    public const string Harakatlar = "Harakatlar";
}

public static class Audit
{
    /// <summary>tur — <see cref="AuditTurlari"/>: smena | nasiya | xarajat | bak | tuzatish | hisob | sozlama | kirish.</summary>
    public static void Yoz(FuelControlDbContext db, string kim, string amal, string tafsilot, string tur) =>
        db.Audit.Add(new AuditYozuvi { Vaqt = DateTime.UtcNow, Kim = kim, Amal = amal, Tafsilot = tafsilot, Tur = tur });
}

public static class Xaritalash
{
    public static FoydalanuvchiDto Dto(this Foydalanuvchi f) =>
        new(f.Id, f.ToliqIsm, f.Login, f.Rol, f.Faol, f.OylikMaosh, f.Ruxsatlar.Distinct().ToArray());

    public static HisobHarakatiDto Dto(this HisobHarakati h) =>
        new(h.Id, h.OperatorId, h.Sana, h.Turi, h.Summa, h.Izoh, h.KimYozdi);
}

public static class MaoshYozuvchi
{
    /// <summary>
    /// Joriy oy uchun maosh yozilmagan faol operatorlarga yozadi (idempotent). Boshqa so'rov/nusxa shu oyni oldinroq
    /// yozgan bo'lsa, IX_Harakatlar_MaoshOyi rad etadi — jim o'tkazib yuboramiz.
    /// </summary>
    public static async Task Yoz(FuelControlDbContext db, DateTime? hozirUtc = null)
    {
        var hozir = hozirUtc ?? DateTime.UtcNow;
        var oyBoshi = new DateTime(hozir.Year, hozir.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var oy = OylikMaoshXizmati.Oy(hozir);
        var operatorlar = await db.Foydalanuvchilar.Where(f => f.Faol && f.Rol == Rol.Operator && f.OylikMaosh > 0).ToListAsync();
        var mavjud = await db.Harakatlar.Where(h => h.Turi == HarakatTuri.Maosh && (h.MaoshOyi == oy || h.Sana >= oyBoshi)).ToListAsync();
        foreach (var h in OylikMaoshXizmati.KerakliYozuvlar(operatorlar, mavjud, hozir).ToList())
        {
            db.Harakatlar.Add(h);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException e) when (e.InnerException is Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 })
            {
                db.Entry(h).State = EntityState.Detached;
            }
        }
    }
}
