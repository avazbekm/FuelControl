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

public static class YoqilgiAparatEndpointlari
{
    public static void YoqilgiAparatUlash(this IEndpointRouteBuilder app)
    {
        var y = app.MapGroup("/yoqilgilar").RequireAuthorization();

        y.MapGet("/", async (FuelControlDbContext db) =>
        {
            var band = await db.Aparatlar.Select(a => a.YoqilgiTuriId).Distinct().ToListAsync();
            return (await db.Yoqilgilar.OrderBy(x => x.Id).ToListAsync()).Select(x => x.Dto(band.Contains(x.Id)));
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
            Audit.Yoz(db, ctx.User.Ism(), "Yoqilg'i yaratildi", $"{e.Nomi} · narx {Format.Pul(e.Narx)}", AuditTurlari.Sozlama);
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Yoqilgilar);
            return Results.Created($"/yoqilgilar/{e.Id}", e.Dto(false));
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<YoqilgiTuriDto>(201);

        // Ochiq smenada narx o'zgarsa, shu yoqilg'i barcha aparatlarining hozirgi pult ko'rsatkichi (Korsatkichlar) majburiy: yetishmasa 400
        // (extensions.kerakliAparatlar). Shu paytgacha sotilgan litr eski narxda alohida segment bo'ladi, qolgani yopishda yangi narxda.
        y.MapPut("/{id:int}", async (int id, YoqilgiTahrirlashDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            Tekshir(s.Nomi, s.Narx);
            try
            {
                var (dto, smena) = await Tranzaksiya.Bajar(db, async () =>
                {
                    var e = await db.Yoqilgilar.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
                    var nomi = s.Nomi.Trim();
                    if (await db.Yoqilgilar.AnyAsync(x => x.Nomi == nomi && x.Id != id)) throw new BiznesXatosi("Bunday yoqilg'i turi bor.", 409);
                    SmenaDto? smenaDto = null;
                    if (e.Narx != s.Narx)
                    {
                        var segmentSoni = 0;
                        var ochiq = await db.Smenalar.FirstOrDefaultAsync(x => x.Tugadi == null);
                        var aparatlar = await db.Aparatlar.Where(a => a.YoqilgiTuriId == id).ToListAsync();
                        if (ochiq is not null && aparatlar.Count > 0)
                        {
                            var mavjud = await db.SmenaKorsatkichlari.Where(x => x.SmenaId == ochiq.Id).ToListAsync();
                            var yangi = SmenaHisoblagich.NarxOzgarishi(ochiq, e.Narx, aparatlar, mavjud, SmenaEndpointlari.Korsatkichlar(s.Korsatkichlar), DateTime.UtcNow);
                            db.SmenaKorsatkichlari.AddRange(yangi);
                            segmentSoni = yangi.Count;
                            smenaDto = await db.SmenaDtosi(ochiq);
                        }
                        db.NarxTarixlari.Add(new NarxTarixi
                        {
                            YoqilgiTuriId = e.Id, YoqilgiNomi = nomi, Vaqt = DateTime.UtcNow, EskiNarx = e.Narx, YangiNarx = s.Narx, Kim = ctx.User.Ism(),
                        });
                        Audit.Yoz(db, ctx.User.Ism(), "Narx o'zgardi", AuditMatnlari.NarxOzgardi(e.Nomi, e.Narx, s.Narx, segmentSoni), AuditTurlari.Sozlama);
                    }
                    e.Nomi = nomi; e.Narx = s.Narx; e.Rang = s.Rang;
                    await db.SaveChangesAsync();
                    return (e.Dto(await db.Aparatlar.AnyAsync(a => a.YoqilgiTuriId == e.Id)), smenaDto);
                });
                await hub.Clients.All.SendAsync(Xabarlar.NarxOzgardi, dto);
                if (smena is not null) await hub.Kuzatuvchilarga(Xabarlar.SmenaOzgardi, smena);
                return Results.Ok(dto);
            }
            catch (KorsatkichKerakXatosi k)
            {
                return Results.Problem(statusCode: 400, title: "Pult ko'rsatkichi kerak", detail: k.Message,
                    extensions: new Dictionary<string, object?> { ["kerakliAparatlar"] = k.KerakliAparatlar });
            }
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<YoqilgiTuriDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        y.MapDelete("/{id:int}", async (int id, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var e = await db.Yoqilgilar.FindAsync(id) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
            if (await db.Aparatlar.AnyAsync(a => a.YoqilgiTuriId == id))
                throw new BiznesXatosi("Aparatga biriktirilgan yoqilg'ini o'chirib bo'lmaydi.", 409);
            db.Yoqilgilar.Remove(e);
            Audit.Yoz(db, ctx.User.Ism(), "Yoqilg'i o'chirildi", e.Nomi, AuditTurlari.Sozlama);
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Yoqilgilar);
            return Results.NoContent();
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces(204);

        var a = app.MapGroup("/aparatlar").RequireAuthorization();

        a.MapGet("/", async (FuelControlDbContext db) => await db.AparatDtolari()).Produces<AparatDto[]>();

        a.MapPost("/", async (AparatYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            if (s.BoshlangichTotalLitr < 0) throw new BiznesXatosi("Totalizator manfiy bo'lmasligi kerak.");
            if (s.BoshlangichBakQoldiq < 0) throw new BiznesXatosi("Bak qoldig'i manfiy bo'lmasligi kerak.");
            var yoqilgi = await db.Yoqilgilar.FindAsync(s.YoqilgiTuriId) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
            if (await db.Aparatlar.AnyAsync(x => x.Raqam == s.Raqam)) throw new BiznesXatosi("Bunday raqamli aparat bor.", 409);
            var e = new Aparat
            {
                Raqam = s.Raqam, YoqilgiTuriId = s.YoqilgiTuriId,
                TotalLitr = SmenaHisoblagich.Yaxlitla(s.BoshlangichTotalLitr), BakQoldiq = SmenaHisoblagich.Yaxlitla(s.BoshlangichBakQoldiq),
            };
            db.Aparatlar.Add(e);
            Audit.Yoz(db, ctx.User.Ism(), "Aparat yaratildi", AuditMatnlari.AparatYaratildi(e, yoqilgi.Nomi), AuditTurlari.Sozlama);
            await db.SaveChangesAsync();
            var dto = e.Dto(yoqilgi.Nomi, null);
            await hub.Bildir(Bolimlar.Aparatlar);
            await hub.Kuzatuvchilarga(Xabarlar.AparatOzgardi, dto);
            return Results.Created($"/aparatlar/{e.Id}", dto);
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<AparatDto>(201).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // TotalLitr yoki BakQoldiq berilsa va farq qilsa - qo'lda tuzatiladi: Sabab majburiy, auditga yoziladi (bak tuzatishi tarixga ham yoziladi).
        a.MapPut("/{id:int}", async (int id, AparatTahrirlashDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var e = await db.Aparatlar.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BiznesXatosi("Aparat topilmadi.", 404);
                var yoqilgi = await db.Yoqilgilar.FindAsync(s.YoqilgiTuriId) ?? throw new BiznesXatosi("Yoqilg'i topilmadi.", 404);
                if (await db.Aparatlar.AnyAsync(x => x.Raqam == s.Raqam && x.Id != id)) throw new BiznesXatosi("Bunday raqamli aparat bor.", 409);
                if (s.TotalLitr < 0) throw new BiznesXatosi("Totalizator manfiy bo'lmasligi kerak.");
                var kim = ctx.User.Ism();
                var hozir = DateTime.UtcNow;

                // Ochiq smenada narx o'zgarishi segmenti bor aparatning ko'rsatkichi/yoqilg'isini smena yopilguncha o'zgartirib bo'lmaydi.
                var ochiqId = await db.Smenalar.Where(x => x.Tugadi == null).Select(x => (int?)x.Id).FirstOrDefaultAsync();
                var segmentBor = ochiqId is { } o && await db.SmenaKorsatkichlari.AnyAsync(x => x.SmenaId == o && x.AparatId == id);
                var yangiTotal = s.TotalLitr is { } t ? SmenaHisoblagich.Yaxlitla(t) : (decimal?)null;
                if (segmentBor && (e.YoqilgiTuriId != s.YoqilgiTuriId || (yangiTotal is { } yt && yt != e.TotalLitr)))
                    throw new BiznesXatosi("Bu aparatda ochiq smenada narx o'zgarishi qayd etilgan: pult ko'rsatkichi yoki yoqilg'ini smena yopilgandan keyin o'zgartiring.", 409);

                if (e.Raqam != s.Raqam || e.YoqilgiTuriId != s.YoqilgiTuriId)
                    Audit.Yoz(db, kim, "Aparat o'zgartirildi", AuditMatnlari.AparatOzgartirildi(e.Raqam, s.Raqam, yoqilgi.Nomi), AuditTurlari.Sozlama);
                if (yangiTotal is { } yangi && yangi != e.TotalLitr)
                {
                    if (string.IsNullOrWhiteSpace(s.Sabab)) throw new BiznesXatosi("Pult ko'rsatkichini tuzatish sababi majburiy.");
                    Audit.Yoz(db, kim, "Totalizator tuzatildi", AuditMatnlari.TotalTuzatildi(s.Raqam, e.TotalLitr, yangi, s.Sabab), AuditTurlari.Tuzatish);
                    e.TotalLitr = yangi;
                }
                if (s.BakQoldiq is { } bak)
                {
                    var eskiBak = e.BakQoldiq;
                    if (BakXizmati.Tuzat(e, bak, s.Sabab, kim, hozir) is { } tuzatish)
                    {
                        db.BakTuzatishlari.Add(tuzatish);
                        Audit.Yoz(db, kim, "Bak qoldig'i tuzatildi", AuditMatnlari.BakTuzatildi(s.Raqam, eskiBak, e.BakQoldiq, s.Sabab!), AuditTurlari.Bak);
                    }
                }
                e.Raqam = s.Raqam; e.YoqilgiTuriId = s.YoqilgiTuriId;
                await db.SaveChangesAsync();
                return await db.AparatDtosi(e);
            });
            await hub.Bildir(Bolimlar.Aparatlar);
            await hub.Kuzatuvchilarga(Xabarlar.AparatOzgardi, dto);
            return Results.Ok(dto);
        }).RuxsatKerak(Ruxsat.Sozlamalar).Produces<AparatDto>().ProducesProblem(400).ProducesProblem(404).ProducesProblem(409);

        // Bakka kirim (zavoddan kelgan yoqilg'i, litrda): bak qoldig'i oshadi, pult ko'rsatkichi o'zgarmaydi; qoldiq oldin/keyin saqlanadi, auditga yoziladi.
        a.MapPost("/{id:int}/kirim", async (int id, BakKirimYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            var dto = await Tranzaksiya.Bajar(db, async () =>
            {
                var e = await db.Aparatlar.FirstOrDefaultAsync(x => x.Id == id) ?? throw new BiznesXatosi("Aparat topilmadi.", 404);
                var nomi = await db.Yoqilgilar.Where(y => y.Id == e.YoqilgiTuriId).Select(y => y.Nomi).FirstOrDefaultAsync() ?? "?";
                var kirim = BakXizmati.Kirim(e, s.Litr, s.Vaqt, s.Hujjat, ctx.User.Ism(), DateTime.UtcNow);
                db.BakKirimlari.Add(kirim);
                Audit.Yoz(db, ctx.User.Ism(), "Bakka kirim", AuditMatnlari.BakKirimi(e.Raqam, nomi, kirim), AuditTurlari.Bak);
                await db.SaveChangesAsync();
                return await db.AparatDtosi(e);
            });
            await hub.Bildir(Bolimlar.Aparatlar);
            await hub.Kuzatuvchilarga(Xabarlar.AparatOzgardi, dto);
            return Results.Ok(dto);
        }).RuxsatKerak(Ruxsat.BakKirim).Produces<AparatDto>().ProducesProblem(400).ProducesProblem(404);

        app.MapGet("/bak-kirimlar", async (FuelControlDbContext db, int? aparatId, DateOnly? dan, DateOnly? gacha) =>
        {
            var q = db.BakKirimlari.AsNoTracking().AsQueryable();
            if (aparatId is { } ai) q = q.Where(k => k.AparatId == ai);
            if (Vaqt.Dan(dan) is { } d) q = q.Where(k => k.Vaqt >= d);
            if (Vaqt.Gacha(gacha) is { } g) q = q.Where(k => k.Vaqt < g);
            var raqamlar = await db.Aparatlar.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Raqam);
            return (await q.OrderByDescending(k => k.Vaqt).ThenByDescending(k => k.Id).Take(2000).ToListAsync())
                .Select(k => k.Dto(raqamlar.GetValueOrDefault(k.AparatId))).ToArray();
        }).RuxsatdanBiriKerak(Ruxsat.Hisobotlar, Ruxsat.BakKirim).RequireAuthorization().Produces<BakKirimDto[]>();
    }

    private static void Tekshir(string nomi, long narx)
    {
        if (string.IsNullOrWhiteSpace(nomi)) throw new BiznesXatosi("Yoqilg'i nomi kiritilishi shart.");
        if (narx <= 0) throw new BiznesXatosi("Narx musbat bo'lishi kerak.");
    }
}
