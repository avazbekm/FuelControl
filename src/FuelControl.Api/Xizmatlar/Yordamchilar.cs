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
        if (u.Bor(Ruxsat.Smenalar) || u.Bor(Ruxsat.Hisobotlar) || u.Bor(Ruxsat.Boshqaruv))
            await Groups.AddToGroupAsync(Context.ConnectionId, Kuzatuvchilar);
        await base.OnConnectedAsync();
    }
}

public static class Xabarlar
{
    public const string SotuvQoshildi = "SotuvQoshildi";
    public const string SotuvOzgardi = "SotuvOzgardi";
    public const string SmenaOzgardi = "SmenaOzgardi";
    public const string NarxOzgardi = "NarxOzgardi";

    /// <summary>Boshqa o'zgarishlar: parametr — bo'lim nomi (Bolimlar.*), klient o'sha ro'yxatni qayta yuklaydi.</summary>
    public const string Ozgardi = "Ozgardi";
}

public static class HubKengaytmasi
{
    public static Task Bildir(this IHubContext<SotuvHub> hub, string bolim) => hub.Clients.All.SendAsync(Xabarlar.Ozgardi, bolim);

    /// <summary>Operatorga tegishli xabar (sotuv, smena) — kuzatuvchilarga va operatorning o'ziga.</summary>
    public static Task OperatorgaBildir(this IHubContext<SotuvHub> hub, int operatorId, string xabar, object dto) =>
        hub.Clients.Groups([SotuvHub.Kuzatuvchilar, SotuvHub.Egasi(operatorId)]).SendAsync(xabar, dto);
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
    public static void Yoz(FuelControlDbContext db, string kim, string amal, string tafsilot) =>
        db.Audit.Add(new AuditYozuvi { Vaqt = DateTime.UtcNow, Kim = kim, Amal = amal, Tafsilot = tafsilot });
}

public static class Xaritalash
{
    public static FoydalanuvchiDto Dto(this Foydalanuvchi f) =>
        new(f.Id, f.ToliqIsm, f.Login, f.Rol, f.Faol, f.OylikMaosh, f.Ruxsatlar.Distinct().ToArray());

    public static SmenaDto Dto(this Smena s, string operatorIsmi) => new(
        s.Id, s.OperatorId, operatorIsmi, s.Boshlandi, s.Tugadi,
        s.KutilganNaqd, s.KutilganPlastik, s.KutilganClick, s.JamiLitr, s.SotuvSoni,
        s.TopshirilganNaqd, s.TopshirilganPlastik, s.TopshirilganClick,
        SmenaHisoblagich.Farq(s), SmenaHisoblagich.Kamomat(s), SmenaHisoblagich.Ortiqcha(s), s.Izoh);

    public static SotuvDto Dto(this Sotuv s, string operatorIsmi, Aparat a, string yoqilgiNomi) => new(
        s.Id, s.SmenaId, s.OperatorId, operatorIsmi, s.AparatId, a.Raqam, yoqilgiNomi,
        s.Narx, s.Litr, s.Summa, s.Vaqt,
        s.Tolovlar.Select(t => new TolovDto(t.Turi, t.Summa)).ToArray(),
        s.Holati, s.BekorSababi, s.BekorQilgan);

    public static HisobHarakatiDto Dto(this HisobHarakati h) =>
        new(h.Id, h.OperatorId, h.Sana, h.Turi, h.Summa, h.Izoh, h.KimYozdi);

    /// <summary>Sotuvlarni DTO'ga aylantiradi (operator, aparat, yoqilg'i nomlarini bitta so'rovda yig'adi).</summary>
    public static async Task<List<SotuvDto>> SotuvDtolari(this FuelControlDbContext db, List<Sotuv> sotuvlar)
    {
        var opIdlar = sotuvlar.Select(s => s.OperatorId).Distinct().ToList();
        var ismlar = await db.Foydalanuvchilar.Where(f => opIdlar.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.ToliqIsm);
        var aparatlar = await db.Aparatlar.ToDictionaryAsync(a => a.Id);
        var yoqilgilar = await db.Yoqilgilar.ToDictionaryAsync(y => y.Id, y => y.Nomi);
        return sotuvlar.Select(s =>
        {
            var a = aparatlar[s.AparatId];
            return s.Dto(ismlar.GetValueOrDefault(s.OperatorId, "?"), a, yoqilgilar[a.YoqilgiTuriId]);
        }).ToList();
    }

    public static async Task<SmenaDto> SmenaDtosi(this FuelControlDbContext db, Smena s)
    {
        var ism = await db.Foydalanuvchilar.Where(f => f.Id == s.OperatorId).Select(f => f.ToliqIsm).FirstAsync();
        return s.Dto(ism);
    }
}

public static class Vaqt
{
    public static readonly TimeSpan Toshkent = TimeSpan.FromHours(5);

    /// <summary>Toshkent kuni boshlanishi (UTC sifatida).</summary>
    public static DateTime KunBoshi(DateTime utc) => utc.Add(Toshkent).Date.Subtract(Toshkent);

    /// <summary>?dan=2026-03-01 kabi Toshkent sanasini UTC chegaraga aylantiradi.</summary>
    public static DateTime? Dan(DateOnly? d) => d is null ? null : DateTime.SpecifyKind(d.Value.ToDateTime(TimeOnly.MinValue) - Toshkent, DateTimeKind.Utc);

    public static DateTime? Gacha(DateOnly? d) => d is null ? null : DateTime.SpecifyKind(d.Value.AddDays(1).ToDateTime(TimeOnly.MinValue) - Toshkent, DateTimeKind.Utc);
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
