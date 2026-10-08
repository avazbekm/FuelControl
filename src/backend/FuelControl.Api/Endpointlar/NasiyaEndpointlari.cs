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

public static class NasiyaEndpointlari
{
    public static void NasiyaUlash(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/nasiyalar").RequireAuthorization();

        // holat: faol (qarzi bor hammasi) | otgan (muddati o'tgan) | yopilgan; bo'sh = hammasi. q - ism / telefon / mashina raqami
        // (qoida: NasiyaQidiruvi, /nasiyalar/mijozlar bilan bir xil). Xulosa (kartalar) filtrga bog'liq emas - hamma nasiyalar bo'yicha.
        g.MapGet("/", async (FuelControlDbContext db, string? holat, string? q) =>
        {
            var hozir = DateTime.UtcNow;
            var bugun = Vaqt.Sana(hozir);
            var barchasi = await db.Nasiyalar.AsNoTracking().ToListAsync();
            var qaytishlar = await db.NasiyaQaytishlari.AsNoTracking().ToListAsync();
            var xulosa = NasiyaXizmati.Xulosa(barchasi, qaytishlar, bugun, Vaqt.OyBoshi(hozir), Vaqt.KeyingiOyBoshi(hozir));
            var royxat = NasiyaXizmati.Royxat(barchasi, holat, q, bugun).Select(n => n.Dto(bugun)).ToArray();
            return new NasiyalarDto(xulosa, royxat);
        }).RuxsatKerak(Ruxsat.Nasiyalar).Produces<NasiyalarDto>().ProducesProblem(400);

        // Nasiya yozish va qarz qaytdi dialoglarida mavjud mijozni topish / avtomatik to'ldirish: nasiya yozuvlaridan yig'iladigan
        // mijozlar (telefon bo'yicha, telefon yo'q bo'lsa ism + mashina raqami bo'yicha), eng ko'pi 8 ta.
        g.MapGet("/mijozlar", async (FuelControlDbContext db, string? q) =>
            NasiyaXizmati.MijozTakliflari(await db.Nasiyalar.AsNoTracking().ToListAsync(), q, NasiyaXizmati.MaksTaklif)
        ).RuxsatdanBiriKerak(Ruxsat.NasiyaYozish, Ruxsat.QarzQaytdi, Ruxsat.Nasiyalar).Produces<MijozTaklifDto[]>();

        g.MapGet("/{id:int}", async (int id, FuelControlDbContext db) =>
        {
            var n = await db.Nasiyalar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw new BiznesXatosi("Nasiya topilmadi.", 404);
            var qaytishlar = await db.NasiyaQaytishlari.AsNoTracking().Where(q => q.NasiyaId == id).OrderBy(q => q.Vaqt).ThenBy(q => q.Id).ToListAsync();
            return Results.Ok(new NasiyaTafsilotDto(n.Dto(Vaqt.Sana(DateTime.UtcNow)), qaytishlar.Select(q => q.Dto(n.MijozIsmi)).ToArray()));
        }).RuxsatKerak(Ruxsat.Nasiyalar).Produces<NasiyaTafsilotDto>().ProducesProblem(404);

        // Ochiq smena majburiy: nasiya shu smenaga yoziladi va kassada bo'lishi kerak bo'lgan naqddan ayiriladi.
        g.MapPost("/", async (NasiyaYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var hozir = DateTime.UtcNow;
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var smenaId = await OchiqSmenaId(db);
                var n = NasiyaXizmati.Yarat(smenaId, ctx.User.FoydalanuvchiId(), ctx.User.Ism(), s.MijozIsmi, s.Telefon, s.MashinaRaqami,
                    s.Summa, s.Muddat, s.Izoh, hozir);
                db.Nasiyalar.Add(n);
                Audit.Yoz(db, ctx.User.Ism(), "Nasiya yozildi", AuditMatnlari.NasiyaYozildi(n), AuditTurlari.Nasiya);
                await db.SaveChangesAsync();
                return n.Dto(Vaqt.Sana(hozir));
            });
            await hub.Kuzatuvchilarga(Xabarlar.NasiyaOzgardi, dto);
            return Results.Created($"/nasiyalar/{dto.Id}", dto);
        }).RuxsatKerak(Ruxsat.NasiyaYozish).Produces<NasiyaDto>(201).ProducesProblem(400).ProducesProblem(409);

        // Qaytish: summa <= qoldiq. SmenaHisobiga=true - ochiq smena majburiy, smena hisobiga kirim; false - smena hisobiga ta'sir qilmaydi
        // (faqat boshliq: "Smenalar" ruxsati - aks holda operator naqd olib, uni smena hisobidan chetda qoldira olardi).
        g.MapPost("/{id:int}/qaytish", async (int id, NasiyaQaytishiYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var hozir = DateTime.UtcNow;
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var n = await db.Nasiyalar.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BiznesXatosi("Nasiya topilmadi.", 404);
                int? smenaId = null;
                if (s.SmenaHisobiga) smenaId = await OchiqSmenaId(db);
                else if (!ctx.User.Bor(Ruxsat.Smenalar))
                    throw new BiznesXatosi("Smena hisobiga yozilmaydigan qaytishni faqat boshliq (\"Smenalar\" ruxsati) yoza oladi.", 403);
                var q = NasiyaXizmati.Qaytish(n, s.Summa, s.Usul, smenaId, ctx.User.FoydalanuvchiId(), ctx.User.Ism(), s.Izoh, hozir);
                db.NasiyaQaytishlari.Add(q);
                Audit.Yoz(db, ctx.User.Ism(), "Qarz qaytdi", AuditMatnlari.QarzQaytdi(n, q), AuditTurlari.Nasiya);
                await db.SaveChangesAsync();
                return n.Dto(Vaqt.Sana(hozir));
            });
            await hub.Kuzatuvchilarga(Xabarlar.NasiyaOzgardi, dto);
            return Results.Ok(dto);
        }).RuxsatKerak(Ruxsat.QarzQaytdi).Produces<NasiyaDto>().ProducesProblem(400).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        // Faqat nasiya yozilgan smena hali ochiq bo'lsa; muallif yoki boshliq ("Smenalar" ruxsati). Qaytishlari bor nasiya o'chirilmaydi.
        g.MapDelete("/{id:int}", async (int id, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var n = await db.Nasiyalar.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BiznesXatosi("Nasiya topilmadi.", 404);
                if (n.OperatorId != ctx.User.FoydalanuvchiId() && !ctx.User.Bor(Ruxsat.Smenalar))
                    throw new BiznesXatosi("Nasiyani faqat uni yozgan foydalanuvchi yoki boshliq o'chira oladi.", 403);
                var ochiq = await db.Smenalar.Where(x => x.Tugadi == null).Select(x => (int?)x.Id).FirstOrDefaultAsync();
                if (ochiq != n.SmenaId) throw new BiznesXatosi("Nasiyani faqat yozilgan smena ochiq bo'lganda o'chirish mumkin.", 409);
                if (await db.NasiyaQaytishlari.AnyAsync(q => q.NasiyaId == id))
                    throw new BiznesXatosi("Qaytishlari bor nasiyani o'chirib bo'lmaydi: avval qaytishlarni o'chiring.", 409);
                var natija = n.Dto(Vaqt.Sana(DateTime.UtcNow));
                db.Nasiyalar.Remove(n);
                Audit.Yoz(db, ctx.User.Ism(), "Nasiya o'chirildi", AuditMatnlari.NasiyaOchirildi(n), AuditTurlari.Nasiya);
                await db.SaveChangesAsync();
                return natija;
            });
            await hub.Kuzatuvchilarga(Xabarlar.NasiyaOzgardi, dto);
            return Results.NoContent();
        }).Produces(204).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);

        // Smena hisobiga yozilgan qaytish - faqat uning smenasi ochiq bo'lsa (muallif yoki boshliq); smenaga bog'lanmagan qaytishni faqat boshliq.
        g.MapDelete("/qaytishlar/{id:int}", async (int id, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var q = await db.NasiyaQaytishlari.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BiznesXatosi("Qaytish topilmadi.", 404);
                var n = await db.Nasiyalar.FirstAsync(x => x.Id == q.NasiyaId);
                var boshliq = ctx.User.Bor(Ruxsat.Smenalar);
                if (q.SmenaId is null)
                {
                    if (!boshliq) throw new BiznesXatosi("Smenaga bog'lanmagan qaytishni faqat boshliq o'chira oladi.", 403);
                }
                else
                {
                    if (q.OperatorId != ctx.User.FoydalanuvchiId() && !boshliq)
                        throw new BiznesXatosi("Qaytishni faqat uni yozgan foydalanuvchi yoki boshliq o'chira oladi.", 403);
                    var ochiq = await db.Smenalar.Where(x => x.Tugadi == null).Select(x => (int?)x.Id).FirstOrDefaultAsync();
                    if (ochiq != q.SmenaId) throw new BiznesXatosi("Qaytishni faqat yozilgan smena ochiq bo'lganda o'chirish mumkin.", 409);
                }
                NasiyaXizmati.QaytishniOlibTashla(n, q);
                db.NasiyaQaytishlari.Remove(q);
                Audit.Yoz(db, ctx.User.Ism(), "Qarz qaytishi o'chirildi", AuditMatnlari.QaytishOchirildi(n, q), AuditTurlari.Nasiya);
                await db.SaveChangesAsync();
                return n.Dto(Vaqt.Sana(DateTime.UtcNow));
            });
            await hub.Kuzatuvchilarga(Xabarlar.NasiyaOzgardi, dto);
            return Results.NoContent();
        }).Produces(204).ProducesProblem(403).ProducesProblem(404).ProducesProblem(409);
    }

    internal static async Task<int> OchiqSmenaId(FuelControlDbContext db) =>
        await db.Smenalar.Where(x => x.Tugadi == null).Select(x => (int?)x.Id).FirstOrDefaultAsync()
        ?? throw new BiznesXatosi("Ochiq smena yo'q: avval smenani oching.", 409);
}
