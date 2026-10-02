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

public static class SmenaSotuvEndpointlari
{
    public static void SmenaSotuvUlash(this IEndpointRouteBuilder app)
    {
        var s = app.MapGroup("/smenalar").RequireAuthorization();

        s.MapGet("/", async (HttpContext ctx, FuelControlDbContext db, DateOnly? dan, DateOnly? gacha, int? operatorId) =>
        {
            // Smenalar ruxsati yo'q foydalanuvchi faqat o'zinikini ko'radi.
            if (!ctx.User.Bor(Ruxsat.Smenalar)) operatorId = ctx.User.FoydalanuvchiId();
            var q = db.Smenalar.AsQueryable();
            if (Vaqt.Dan(dan) is { } d) q = q.Where(x => x.Boshlandi >= d);
            if (Vaqt.Gacha(gacha) is { } g) q = q.Where(x => x.Boshlandi < g);
            if (operatorId is { } o) q = q.Where(x => x.OperatorId == o);
            var royxat = await q.OrderByDescending(x => x.Boshlandi).ToListAsync();
            var ismlar = await db.Foydalanuvchilar.ToDictionaryAsync(f => f.Id, f => f.ToliqIsm);
            return royxat.Select(x => x.Dto(ismlar.GetValueOrDefault(x.OperatorId, "?")));
        });

        s.MapGet("/{id:int}", async (int id, HttpContext ctx, FuelControlDbContext db) =>
        {
            var smena = await db.Smenalar.FindAsync(id) ?? throw new BiznesXatosi("Smena topilmadi.", 404);
            if (smena.OperatorId != ctx.User.FoydalanuvchiId() && !ctx.User.Bor(Ruxsat.Smenalar))
                throw new BiznesXatosi("Bu smenani ko'rish uchun \"Smenalar\" ruxsati kerak.", 403);
            var sotuvlar = await db.Sotuvlar.Include(x => x.Tolovlar).Where(x => x.SmenaId == id).OrderByDescending(x => x.Vaqt).ToListAsync();
            return Results.Ok(new SmenaTafsilotDto(await db.SmenaDtosi(smena), (await db.SotuvDtolari(sotuvlar)).ToArray()));
        }).Produces<SmenaTafsilotDto>();

        s.MapGet("/joriy", async (HttpContext ctx, FuelControlDbContext db) =>
        {
            var id = ctx.User.FoydalanuvchiId();
            var smena = await db.Smenalar.FirstOrDefaultAsync(x => x.OperatorId == id && x.Tugadi == null);
            return smena is null ? Results.NoContent() : Results.Ok(await db.SmenaDtosi(smena));
        }).Produces<SmenaDto>().Produces(204);

        s.MapPost("/och", async (HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var id = ctx.User.FoydalanuvchiId();
            if (await db.Smenalar.AnyAsync(x => x.OperatorId == id && x.Tugadi == null))
                throw new BiznesXatosi("Sizda allaqachon ochiq smena bor.", 409);
            var (smena, yangi) = await SotuvXizmati.SmenaOch(db, id, "");
            if (!yangi) throw new BiznesXatosi("Sizda allaqachon ochiq smena bor.", 409);
            var dto = await db.SmenaDtosi(smena);
            await hub.OperatorgaBildir(dto.OperatorId, Xabarlar.SmenaOzgardi, dto);
            return Results.Created($"/smenalar/{smena.Id}", dto);
        }).RuxsatKerak(Ruxsat.SmenaOchish).Produces<SmenaDto>(201);

        s.MapPost("/{id:int}/yop", async (int id, SmenaYopishDto so, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var smena = await db.Smenalar.FindAsync(id) ?? throw new BiznesXatosi("Smena topilmadi.", 404);
            if (smena.OperatorId != ctx.User.FoydalanuvchiId() && !ctx.User.Bor(Ruxsat.Smenalar))
                throw new BiznesXatosi("Faqat o'z smenangizni yopishingiz mumkin.", 403);
            if (!smena.Ochiqmi) throw new BiznesXatosi("Smena allaqachon yopilgan.", 409);

            await SotuvXizmati.SmenaniQaytaHisobla(db, smena);
            SmenaHisoblagich.Yop(smena, so.Naqd, so.Plastik, so.Click, so.Izoh, DateTime.UtcNow);

            var farq = SmenaHisoblagich.Farq(smena);
            if (farq != 0)
            {
                db.Harakatlar.Add(new HisobHarakati
                {
                    OperatorId = smena.OperatorId, Sana = smena.Tugadi!.Value,
                    Turi = farq < 0 ? HarakatTuri.Kamomat : HarakatTuri.Ortiqcha,
                    Summa = farq, Izoh = $"Smena #{smena.Id} {(farq < 0 ? "kamomati" : "ortiqchasi")}", KimYozdi = "Tizim",
                });
            }
            Audit.Yoz(db, ctx.User.Ism(), "Smena yopildi",
                $"#{smena.Id} kutilgan {SmenaHisoblagich.KutilganJami(smena)}, topshirilgan {SmenaHisoblagich.TopshirilganJami(smena)}, farq {farq}");
            await db.SaveChangesAsync();
            var dto = await db.SmenaDtosi(smena);
            await hub.OperatorgaBildir(dto.OperatorId, Xabarlar.SmenaOzgardi, dto);
            return Results.Ok(dto);
        }).RuxsatKerak(Ruxsat.SmenaYopish).Produces<SmenaDto>();

        var t = app.MapGroup("/sotuvlar").RequireAuthorization();

        t.MapPost("/", async (SotuvYaratishDto so, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var mavjud = await db.Sotuvlar.Include(x => x.Tolovlar).FirstOrDefaultAsync(x => x.IdempotencyKey == so.IdempotencyKey);
            if (mavjud is not null) return Results.Ok((await db.SotuvDtolari([mavjud]))[0]);

            var (sotuv, yangi) = await SotuvXizmati.Yarat(db, ctx.User.FoydalanuvchiId(), so);
            var dto = (await db.SotuvDtolari([sotuv]))[0];
            if (!yangi) return Results.Ok(dto);
            await hub.OperatorgaBildir(dto.OperatorId, Xabarlar.SotuvQoshildi, dto);
            return Results.Created($"/sotuvlar/{sotuv.Id}", dto);
        }).RuxsatKerak(Ruxsat.SotuvKiritish).Produces<SotuvDto>().Produces<SotuvDto>(201);

        t.MapGet("/", async (HttpContext ctx, FuelControlDbContext db, DateOnly? dan, DateOnly? gacha, int? operatorId, int? smenaId, SotuvHolati? holati) =>
        {
            if (!ctx.User.Bor(Ruxsat.Smenalar) && !ctx.User.Bor(Ruxsat.Hisobotlar) && !ctx.User.Bor(Ruxsat.Boshqaruv))
                operatorId = ctx.User.FoydalanuvchiId();
            var q = db.Sotuvlar.Include(x => x.Tolovlar).AsQueryable();
            if (Vaqt.Dan(dan) is { } d) q = q.Where(x => x.Vaqt >= d);
            if (Vaqt.Gacha(gacha) is { } g) q = q.Where(x => x.Vaqt < g);
            if (operatorId is { } o) q = q.Where(x => x.OperatorId == o);
            if (smenaId is { } sm) q = q.Where(x => x.SmenaId == sm);
            if (holati is { } h) q = q.Where(x => x.Holati == h);
            return await db.SotuvDtolari(await q.OrderByDescending(x => x.Vaqt).ToListAsync());
        });

        t.MapPut("/{id:int}", async (int id, SotuvTahrirlashDto so, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var sotuv = await SotuvXizmati.Tahrirla(db, id, so, ctx.User.Ism());
            var dto = (await db.SotuvDtolari([sotuv]))[0];
            await hub.OperatorgaBildir(dto.OperatorId, Xabarlar.SotuvOzgardi, dto);
            return Results.Ok(dto);
        }).RuxsatKerak(Ruxsat.SotuvTahrirlash).Produces<SotuvDto>();

        t.MapPost("/{id:int}/bekor", async (int id, SotuvBekorQilishDto so, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var sotuv = await SotuvXizmati.BekorQil(db, id, so.Sabab, ctx.User.Ism());
            var dto = (await db.SotuvDtolari([sotuv]))[0];
            await hub.OperatorgaBildir(dto.OperatorId, Xabarlar.SotuvOzgardi, dto);
            return Results.Ok(dto);
        }).RuxsatKerak(Ruxsat.SotuvBekorQilish).Produces<SotuvDto>();
    }
}
