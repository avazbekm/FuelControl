using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using FuelControl.Api.Xizmatlar;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Endpointlar;

public static class XarajatEndpointlari
{
    public static void XarajatUlash(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/xarajatlar").RequireAuthorization();

        // "Smenalar" ruxsati yo'q foydalanuvchi faqat o'zi ochgan smenalarning xarajatlarini ko'radi.
        g.MapGet("/", async (HttpContext ctx, FuelControlDbContext db, int? smenaId, DateOnly? dan, DateOnly? gacha) =>
        {
            var q = db.Xarajatlar.AsNoTracking().AsQueryable();
            if (!ctx.User.Bor(Ruxsat.Smenalar))
            {
                var men = ctx.User.FoydalanuvchiId();
                q = q.Where(x => db.Smenalar.Any(s => s.Id == x.SmenaId && s.OperatorId == men));
            }
            if (smenaId is { } sm) q = q.Where(x => x.SmenaId == sm);
            if (Vaqt.Dan(dan) is { } d) q = q.Where(x => x.Vaqt >= d);
            if (Vaqt.Gacha(gacha) is { } e) q = q.Where(x => x.Vaqt < e);
            return (await q.OrderByDescending(x => x.Vaqt).ThenByDescending(x => x.Id).ToListAsync()).Select(x => x.Dto()).ToArray();
        }).Produces<XarajatDto[]>();

        // Ochiq smena majburiy. Manba Kassa yoki Depozit karta; boshliq kassadan naqd olib ketsa ham xarajat sifatida yoziladi.
        g.MapPost("/", async (XarajatYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var smenaId = await NasiyaEndpointlari.OchiqSmenaId(db);
                var x = XarajatXizmati.Yarat(smenaId, ctx.User.FoydalanuvchiId(), ctx.User.Ism(), s.Summa, s.Sabab, s.Manba, DateTime.UtcNow);
                db.Xarajatlar.Add(x);
                Audit.Yoz(db, ctx.User.Ism(), "Xarajat yozildi", AuditMatnlari.Xarajat(x), AuditTurlari.Xarajat);
                await db.SaveChangesAsync();
                return x.Dto();
            });
            await hub.Kuzatuvchilarga(Xabarlar.XarajatOzgardi, dto);
            return Results.Created($"/xarajatlar/{dto.Id}", dto);
        }).RuxsatKerak(Ruxsat.XarajatYozish).Produces<XarajatDto>(201).ProducesProblem(400).ProducesProblem(409);

        // Faqat ochiq smenada; muallif yoki boshliq ("Smenalar" ruxsati).
        g.MapDelete("/{id:int}", async (int id, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var x = await db.Xarajatlar.FirstOrDefaultAsync(e => e.Id == id) ?? throw new BiznesXatosi("Xarajat topilmadi.", 404);
                if (x.OperatorId != ctx.User.FoydalanuvchiId() && !ctx.User.Bor(Ruxsat.Smenalar))
                    throw new BiznesXatosi("Xarajatni faqat uni yozgan foydalanuvchi yoki boshliq o'chira oladi.", 403);
                var ochiq = await db.Smenalar.Where(s => s.Tugadi == null).Select(s => (int?)s.Id).FirstOrDefaultAsync();
                if (ochiq != x.SmenaId) throw new BiznesXatosi("Xarajatni faqat yozilgan smena ochiq bo'lganda o'chirish mumkin.", 409);
                var natija = x.Dto();
                db.Xarajatlar.Remove(x);
                Audit.Yoz(db, ctx.User.Ism(), "Xarajat o'chirildi", AuditMatnlari.Xarajat(x), AuditTurlari.Xarajat);
                await db.SaveChangesAsync();
                return natija;
            });
            await hub.Kuzatuvchilarga(Xabarlar.XarajatOzgardi, dto);
            return Results.NoContent();
        }).Produces(204).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }
}
