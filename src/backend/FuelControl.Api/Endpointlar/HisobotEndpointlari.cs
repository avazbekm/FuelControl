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

public static class HisobotEndpointlari
{
    /// <summary>?guruh=smena|kun|oy|operator - katta-kichik harfga bog'liq emas; bo'sh = smena. Sonlar ("7") va noma'lum nomlar - 400.</summary>
    public static HisobotGuruhi GuruhniOqi(string? guruh)
    {
        // Enum.TryParse sonlarni ham qabul qiladi ("7" -> aniqlanmagan qiymat) - faqat nomlar.
        if (!Enum.TryParse<HisobotGuruhi>(string.IsNullOrWhiteSpace(guruh) ? "Smena" : guruh, true, out var qiymat)
            || !Enum.IsDefined(qiymat) || int.TryParse(guruh, out _))
            throw new BiznesXatosi("guruh faqat smena, kun, oy yoki operator bo'lishi mumkin.");
        return qiymat;
    }

    public static void HisobotUlash(this IEndpointRouteBuilder app)
    {
        // Faqat yopilgan smenalar; kunlik/oylik guruhlashda smena ochilgan Toshkent sanasiga yoziladi. Bak jadvali butun shoxobcha bo'yicha
        // (operator filtri unga ta'sir qilmaydi), qatorlar va Jami - operator bo'yicha ham filtrlanadi.
        app.MapGet("/hisobot", async (FuelControlDbContext db, DateOnly? dan, DateOnly? gacha, int? operatorId, string? guruh) =>
        {
            var g = GuruhniOqi(guruh);
            var danUtc = Vaqt.Dan(dan);
            var gachaUtc = Vaqt.Gacha(gacha);

            // dan'dan boshlab hamma yopilgan smenalar (davrdan keyingilari ham: bak qoldig'ini davr oxiriga qaytarish uchun kerak).
            var yopilganlar = await db.Smenalar.AsNoTracking().Where(x => x.Tugadi != null && (danUtc == null || x.Boshlandi >= danUtc)).ToListAsync();
            var davr = yopilganlar.Where(x => gachaUtc == null || x.Boshlandi < gachaUtc);
            var qatorSmenalari = (operatorId is { } o ? davr.Where(x => x.OperatorId == o) : davr).ToList();

            var segmentlar = await db.SmenaKorsatkichlari.AsNoTracking()
                .Where(k => db.Smenalar.Any(s => s.Id == k.SmenaId && s.Tugadi != null && (danUtc == null || s.Boshlandi >= danUtc))).ToListAsync();
            var kirimlar = await db.BakKirimlari.AsNoTracking().Where(k => danUtc == null || k.Vaqt >= danUtc).ToListAsync();
            var tuzatishlar = await db.BakTuzatishlari.AsNoTracking().Where(t => danUtc == null || t.Vaqt >= danUtc).ToListAsync();
            var aparatlar = await db.Aparatlar.AsNoTracking().ToListAsync();
            var yoqilgilar = await db.Yoqilgilar.AsNoTracking().ToDictionaryAsync(y => y.Id, y => y.Nomi);

            var hq = db.Harakatlar.AsNoTracking().Where(h => h.Turi == HarakatTuri.Avans);
            if (danUtc is { } d) hq = hq.Where(h => h.Sana >= d);
            if (gachaUtc is { } e) hq = hq.Where(h => h.Sana < e);
            if (operatorId is { } oid) hq = hq.Where(h => h.OperatorId == oid);
            var avans = -(await hq.Select(h => h.Summa).ToListAsync()).Sum();

            var bak = HisobotXizmati.Aparatlar(aparatlar, yoqilgilar, yopilganlar, segmentlar, kirimlar, tuzatishlar, danUtc, gachaUtc);
            var xarajatSonlari = await db.Xarajatlar.AsNoTracking().GroupBy(x => x.SmenaId)
                .Select(x => new { SmenaId = x.Key, Soni = x.Count() }).ToDictionaryAsync(x => x.SmenaId, x => x.Soni);
            return HisobotXizmati.Tuz(g, qatorSmenalari, await db.OperatorIsmlari(), bak, avans, xarajatSonlari);
        }).RuxsatKerak(Ruxsat.Hisobotlar).RequireAuthorization().Produces<HisobotDto>().ProducesProblem(400);

        app.MapGet("/boshqaruv", async (FuelControlDbContext db) =>
        {
            var hozir = DateTime.UtcNow;
            var oyBoshi = Vaqt.OyBoshi(hozir);
            var ochiq = await db.Smenalar.AsNoTracking().FirstOrDefaultAsync(x => x.Tugadi == null);
            var oxirgilar = await db.Smenalar.AsNoTracking().Where(x => x.Tugadi != null)
                .OrderByDescending(x => x.Boshlandi).ThenByDescending(x => x.Id).Take(14).ToListAsync();
            var oyYopilgan = await db.Smenalar.AsNoTracking().Where(x => x.Tugadi != null && x.Boshlandi >= oyBoshi).ToListAsync();
            var oy = BoshqaruvXizmati.Oy(oyYopilgan);
            var nasiyalar = NasiyaXizmati.Xulosa(await db.Nasiyalar.AsNoTracking().ToListAsync(), await db.NasiyaQaytishlari.AsNoTracking().ToListAsync(),
                Vaqt.Sana(hozir), oyBoshi, Vaqt.KeyingiOyBoshi(hozir));
            var uch = await db.SmenaDtolari(oxirgilar.Take(3).ToList());
            return new BoshqaruvDto(
                ochiq is null ? null : await db.SmenaDtosi(ochiq), uch.FirstOrDefault(),
                oy.Savdo, oy.Litr, oy.SmenaSoni, oy.Kamomat, oy.Ortiqcha,
                nasiyalar, (await db.AparatDtolari()).ToArray(),
                BoshqaruvXizmati.OxirgiSmenalar(oxirgilar, await db.OperatorIsmlari()),
                oy.Tolovlar, uch.ToArray());
        }).RuxsatKerak(Ruxsat.Boshqaruv).RequireAuthorization().Produces<BoshqaruvDto>();

        var op = app.MapGroup("/operatorlar").RequireAuthorization();

        op.MapGet("/{id:int}/hisob", async (int id, HttpContext ctx, FuelControlDbContext db, DateOnly? oy) =>
        {
            // Operator o'z hisobini ko'radi; boshqalarniki uchun Operatorlar ruxsati kerak.
            if (id != ctx.User.FoydalanuvchiId() && !ctx.User.Bor(Ruxsat.Operatorlar))
                return Results.Problem(statusCode: 403, title: "Ruxsat yo'q", detail: "Bu amal uchun \"Operatorlar\" ruxsati kerak.");
            var f = await db.Foydalanuvchilar.FindAsync(id) ?? throw new BiznesXatosi("Operator topilmadi.", 404);
            await MaoshYozuvchi.Yoz(db);
            var barchasi = await db.Harakatlar.Where(h => h.OperatorId == id).OrderByDescending(h => h.Sana).ThenByDescending(h => h.Id).ToListAsync();
            var tanlangan = barchasi;
            long oyJami = 0;
            if (oy is { } o)
            {
                var d = Vaqt.Dan(new DateOnly(o.Year, o.Month, 1))!.Value;
                var g = d.AddMonths(1);
                tanlangan = barchasi.Where(h => h.Sana >= d && h.Sana < g).ToList();
                oyJami = tanlangan.Sum(h => h.Summa);
            }

            // Oylik savdo - joriy (Toshkent) oyda ochilgan yopilgan smenalardan; smenalar soni - ochiqlari bilan.
            var joriyOy = Vaqt.OyBoshi(DateTime.UtcNow);
            var oySavdo = await db.Smenalar.Where(s => s.OperatorId == id && s.Tugadi != null && s.Boshlandi >= joriyOy).SumAsync(s => s.Savdo);
            var oySmenalar = await db.Smenalar.CountAsync(s => s.OperatorId == id && s.Boshlandi >= joriyOy);

            return Results.Ok(new OperatorHisobDto(f.Id, f.ToliqIsm, f.OylikMaosh, oyJami, barchasi.Sum(h => h.Summa),
                oySavdo, oySmenalar, tanlangan.Select(h => h.Dto()).ToArray()));
        }).Produces<OperatorHisobDto>();

        op.MapPost("/{id:int}/harakat", async (int id, HarakatYaratishDto s, HttpContext ctx, FuelControlDbContext db, IHubContext<SotuvHub> hub) =>
        {
            if (s.Turi is not (HarakatTuri.Avans or HarakatTuri.Tolov)) throw new BiznesXatosi("Faqat Avans yoki To'lov yozish mumkin.");
            if (s.Summa <= 0) throw new BiznesXatosi("Summa musbat bo'lishi kerak.");
            var f = await db.Foydalanuvchilar.FindAsync(id) ?? throw new BiznesXatosi("Operator topilmadi.", 404);
            var h = new HisobHarakati
            {
                OperatorId = id, Sana = DateTime.UtcNow, Turi = s.Turi, Summa = -s.Summa,
                Izoh = s.Izoh ?? "", KimYozdi = ctx.User.Ism(),
            };
            db.Harakatlar.Add(h);
            Audit.Yoz(db, ctx.User.Ism(), s.Turi == HarakatTuri.Avans ? "Avans berildi" : "To'lov berildi",
                AuditMatnlari.Avans(f.ToliqIsm, s.Summa, s.Izoh), AuditTurlari.Hisob);
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Harakatlar);
            return Results.Created($"/operatorlar/{id}/hisob", h.Dto());
        }).RuxsatKerak(Ruxsat.AvansBerish).Produces<HisobHarakatiDto>(201);

        // tur: smena | nasiya | xarajat | bak | tuzatish | hisob | sozlama | kirish (noma'lum - 400).
        app.MapGet("/audit", async (FuelControlDbContext db, string? q, string? tur, DateOnly? dan, DateOnly? gacha, int? limit) =>
        {
            var aq = db.Audit.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(tur))
            {
                var t = tur.Trim().ToLowerInvariant();
                if (!AuditTurlari.Hammasi.Contains(t)) throw new BiznesXatosi($"tur faqat {string.Join(", ", AuditTurlari.Hammasi)} bo'lishi mumkin.");
                aq = aq.Where(x => x.Tur == t);
            }
            if (Vaqt.Dan(dan) is { } d) aq = aq.Where(x => x.Vaqt >= d);
            if (Vaqt.Gacha(gacha) is { } g) aq = aq.Where(x => x.Vaqt < g);
            if (!string.IsNullOrWhiteSpace(q)) aq = aq.Where(x => x.Kim.Contains(q) || x.Amal.Contains(q) || x.Tafsilot.Contains(q));
            return (await aq.OrderByDescending(x => x.Vaqt).ThenByDescending(x => x.Id).Take(Math.Clamp(limit ?? 1000, 1, 5000)).ToListAsync())
                .Select(x => new AuditYozuviDto(x.Id, x.Vaqt, x.Kim, x.Amal, x.Tafsilot, x.Tur));
        }).RuxsatKerak(Ruxsat.Audit).RequireAuthorization().Produces<AuditYozuviDto[]>().ProducesProblem(400);

        // Eksport klientda bajariladi (Excel fayl foydalanuvchi kompyuterida) - server faqat auditga yozadi.
        app.MapPost("/audit/eksport", async (AuditEksportDto s, HttpContext ctx, FuelControlDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(s.Turi)) throw new BiznesXatosi("Eksport turi kiritilishi shart.");
            Audit.Yoz(db, ctx.User.Ism(), $"Eksport: {s.Turi.Trim()}", s.Tafsilot ?? "",
                s.Turi.Contains("Hisob-varaqa", StringComparison.OrdinalIgnoreCase) ? AuditTurlari.Hisob : AuditTurlari.Sozlama);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RuxsatKerak(Ruxsat.Eksport).RequireAuthorization().Produces(204);
    }
}
