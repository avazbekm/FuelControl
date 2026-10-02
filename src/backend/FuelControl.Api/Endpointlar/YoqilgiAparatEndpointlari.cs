using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using FuelControl.Api.Xizmatlar;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Endpointlar;

public static class YoqilgiAparatEndpointlari
{
    public static void YoqilgiAparatUlash(this IEndpointRouteBuilder app)
    {
        var y = app.MapGroup("/yoqilgilar").RequireAuthorization();

        y.MapGet("/", async (FuelControlDbContext db) =>
        {
            var band = await db.Aparatlar.Select(a => a.YoqilgiTuriId).Distinct().ToListAsync();
            return (await db.Yoqilgilar.OrderBy(x => x.Id).ToListAsync())
                .Select(x => new YoqilgiTuriDto(x.Id, x.Nomi, x.Narx, x.Rang, band.Contains(x.Id)));
        });

        y.MapGet("/narx-tarixi", async (FuelControlDbContext db) =>
            (await db.NarxTarixlari.OrderByDescending(n => n.Vaqt).ToListAsync())
                .Select(n => new NarxTarixiDto(n.Vaqt, n.YoqilgiNomi, n.EskiNarx, n.YangiNarx, n.Kim)));

        y.MapPost("/", async (YoqilgiYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            Tekshir(s.Nomi, s.Narx);
            if (await db.Yoqilgilar.AnyAsync(x => x.Nomi == s.Nomi)) throw new BiznesXatosi("Bunday yoqilg'i turi bor.", 409);
            var e = new YoqilgiTuri { Nomi = s.Nomi.Trim(), Narx = s.Narx, Rang = s.Rang };
            db.Yoqilgilar.Add(e);
            Audit.Yoz(db, ctx.User.Ism(), "Yoqilg'i yaratildi", $"{e.Nomi}, narx {e.Narx}");
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Yoqilgilar);
            return Results.Created($"/yoqilgilar/{e.Id}", new YoqilgiTuriDto(e.Id, e.Nomi, e.Narx, e.Rang, false));
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<YoqilgiTuriDto>(201);

        y.MapPut("/{id:int}", async (int id, YoqilgiTahrirlashDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            Tekshir(s.Nomi, s.Narx);
            var e = await db.Yoqilgilar.FindAsync(id) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
            if (e.Narx != s.Narx)
            {
                db.NarxTarixlari.Add(new NarxTarixi
                {
                    YoqilgiTuriId = e.Id, YoqilgiNomi = s.Nomi.Trim(), Vaqt = DateTime.UtcNow,
                    EskiNarx = e.Narx, YangiNarx = s.Narx, Kim = ctx.User.Ism(),
                });
                Audit.Yoz(db, ctx.User.Ism(), "Narx o'zgartirildi", $"{e.Nomi}: {e.Narx} → {s.Narx}");
            }
            e.Nomi = s.Nomi.Trim(); e.Narx = s.Narx; e.Rang = s.Rang;
            await db.SaveChangesAsync();
            var dto = new YoqilgiTuriDto(e.Id, e.Nomi, e.Narx, e.Rang, await db.Aparatlar.AnyAsync(a => a.YoqilgiTuriId == e.Id));
            await hub.Clients.All.SendAsync(Xabarlar.NarxOzgardi, dto);
            return Results.Ok(dto);
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<YoqilgiTuriDto>();

        y.MapDelete("/{id:int}", async (int id, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var e = await db.Yoqilgilar.FindAsync(id) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
            if (await db.Aparatlar.AnyAsync(a => a.YoqilgiTuriId == id))
                throw new BiznesXatosi("Aparatga biriktirilgan yoqilg'ini o'chirib bo'lmaydi.", 409);
            db.Yoqilgilar.Remove(e);
            Audit.Yoz(db, ctx.User.Ism(), "Yoqilg'i o'chirildi", e.Nomi);
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Yoqilgilar);
            return Results.NoContent();
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces(204);

        var a = app.MapGroup("/aparatlar").RequireAuthorization();

        a.MapGet("/", async (FuelControlDbContext db) =>
        {
            var nomlar = await db.Yoqilgilar.ToDictionaryAsync(x => x.Id, x => x.Nomi);
            return (await db.Aparatlar.OrderBy(x => x.Raqam).ToListAsync())
                .Select(x => new AparatDto(x.Id, x.Raqam, x.YoqilgiTuriId, nomlar[x.YoqilgiTuriId], x.TotalLitr));
        });

        a.MapPost("/", async (AparatYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            if (s.BoshlangichTotalLitr < 0) throw new BiznesXatosi("Totalizator manfiy bo'lmasligi kerak.");
            var yoqilgi = await db.Yoqilgilar.FindAsync(s.YoqilgiTuriId) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
            if (await db.Aparatlar.AnyAsync(x => x.Raqam == s.Raqam)) throw new BiznesXatosi("Bunday raqamli aparat bor.", 409);
            var e = new Aparat { Raqam = s.Raqam, YoqilgiTuriId = s.YoqilgiTuriId, TotalLitr = s.BoshlangichTotalLitr };
            db.Aparatlar.Add(e);
            Audit.Yoz(db, ctx.User.Ism(), "Aparat yaratildi", $"#{e.Raqam} {yoqilgi.Nomi}, totalizator {e.TotalLitr} L");
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Aparatlar);
            return Results.Created($"/aparatlar/{e.Id}", new AparatDto(e.Id, e.Raqam, e.YoqilgiTuriId, yoqilgi.Nomi, e.TotalLitr));
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<AparatDto>(201);

        a.MapPut("/{id:int}", async (int id, AparatTahrirlashDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var e = await db.Aparatlar.FindAsync(id) ?? throw new BiznesXatosi("Aparat topilmadi.", 404);
            var yoqilgi = await db.Yoqilgilar.FindAsync(s.YoqilgiTuriId) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
            if (await db.Aparatlar.AnyAsync(x => x.Raqam == s.Raqam && x.Id != id)) throw new BiznesXatosi("Bunday raqamli aparat bor.", 409);
            if (s.TotalLitr < 0) throw new BiznesXatosi("Totalizator manfiy bo'lmasligi kerak.");
            if (e.Raqam != s.Raqam || e.YoqilgiTuriId != s.YoqilgiTuriId)
                Audit.Yoz(db, ctx.User.Ism(), "Aparat o'zgartirildi", $"#{e.Raqam} → #{s.Raqam}, yoqilg'i {yoqilgi.Nomi}");
            if (s.TotalLitr is { } yangiTotal && yangiTotal != e.TotalLitr)
            {
                Audit.Yoz(db, ctx.User.Ism(), "Totalizator tuzatildi", $"#{s.Raqam}: {e.TotalLitr} L → {yangiTotal} L");
                e.TotalLitr = yangiTotal;
            }
            e.Raqam = s.Raqam; e.YoqilgiTuriId = s.YoqilgiTuriId;
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Aparatlar);
            return Results.Ok(new AparatDto(e.Id, e.Raqam, e.YoqilgiTuriId, yoqilgi.Nomi, e.TotalLitr));
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<AparatDto>();
    }

    private static void Tekshir(string nomi, long narx)
    {
        if (string.IsNullOrWhiteSpace(nomi)) throw new BiznesXatosi("Yoqilg'i nomi kiritilishi shart.");
        if (narx <= 0) throw new BiznesXatosi("Narx musbat bo'lishi kerak.");
    }
}
