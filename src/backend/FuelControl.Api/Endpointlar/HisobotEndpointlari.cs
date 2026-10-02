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
    public static void HisobotUlash(this IEndpointRouteBuilder app)
    {
        app.MapGet("/hisobot", async (FuelControlDbContext db, DateOnly? dan, DateOnly? gacha, int? operatorId, string? guruh) =>
        {
            // ?guruh=operator|kun|oy — katta-kichik harfga bog'liq emas
            // Enum.TryParse sonlarni ham qabul qiladi ("7" → aniqlanmagan qiymat) — faqat nomlar.
            if (!Enum.TryParse<HisobotGuruhi>(guruh ?? "Operator", true, out var guruhQiymati)
                || !Enum.IsDefined(guruhQiymati) || int.TryParse(guruh, out _))
                throw new BiznesXatosi("guruh faqat operator, kun yoki oy bo'lishi mumkin.");
            var g2 = guruhQiymati;
            var d = Vaqt.Dan(dan); var g = Vaqt.Gacha(gacha);
            var sq = db.Sotuvlar.Include(x => x.Tolovlar).AsQueryable();
            var hq = db.Harakatlar.AsQueryable();
            if (d is not null) { sq = sq.Where(x => x.Vaqt >= d); hq = hq.Where(x => x.Sana >= d); }
            if (g is not null) { sq = sq.Where(x => x.Vaqt < g); hq = hq.Where(x => x.Sana < g); }
            if (operatorId is { } o) { sq = sq.Where(x => x.OperatorId == o); hq = hq.Where(x => x.OperatorId == o); }
            var sotuvlar = await sq.ToListAsync();
            var harakatlar = await hq.ToListAsync();
            var ismlar = await db.Foydalanuvchilar.ToDictionaryAsync(f => f.Id, f => f.ToliqIsm);
            var aparatlar = await db.Aparatlar.ToDictionaryAsync(a => a.Id, a => a.YoqilgiTuriId);
            var yoqilgilar = await db.Yoqilgilar.ToDictionaryAsync(y => y.Id, y => y.Nomi);
            string YoqilgiNomi(Sotuv s) => yoqilgilar[aparatlar[s.AparatId]];

            // Guruh kaliti: operator bo'yicha — Id (nomi bir xil ikki operator aralashmasin), kun/oy — Toshkent sanasi.
            string Kalit(DateTime utc, int op) => g2 switch
            {
                HisobotGuruhi.Kun => utc.Add(Vaqt.Toshkent).ToString("yyyy-MM-dd"),
                HisobotGuruhi.Oy => utc.Add(Vaqt.Toshkent).ToString("yyyy-MM"),
                _ => op.ToString(),
            };

            HisobotQatoriDto Qator(string nom, string? yoqilgi, IEnumerable<Sotuv> s, IEnumerable<HisobHarakati> h)
            {
                var sl = s.ToList(); var faol = sl.Where(x => x.Faolmi).ToList(); var hl = h.ToList();
                return new HisobotQatoriDto(nom, yoqilgi,
                    faol.Sum(x => x.Litr), faol.Sum(x => x.Summa),
                    faol.Sum(x => SmenaHisoblagich.ToloviYigindisi(x, TolovTuri.Naqd)),
                    faol.Sum(x => SmenaHisoblagich.ToloviYigindisi(x, TolovTuri.Plastik)),
                    faol.Sum(x => SmenaHisoblagich.ToloviYigindisi(x, TolovTuri.Click)),
                    faol.Count,
                    -hl.Where(x => x.Turi == HarakatTuri.Kamomat).Sum(x => x.Summa),
                    -hl.Where(x => x.Turi == HarakatTuri.Avans).Sum(x => x.Summa),
                    sl.Count(x => !x.Faolmi),
                    Jami: yoqilgi is null);
            }

            // Guruh qiymati tilga bog'liq emas: kun "yyyy-MM-dd", oy "yyyy-MM", operator — ismi. Klient o'z tilida formatlaydi.
            string GuruhNomi(string kalit) => g2 == HisobotGuruhi.Operator ? ismlar.GetValueOrDefault(int.Parse(kalit), "?") : kalit;

            var sotuvGuruhlari = sotuvlar.ToLookup(x => Kalit(x.Vaqt, x.OperatorId));
            var harakatGuruhlari = harakatlar.ToLookup(x => Kalit(x.Sana, x.OperatorId));
            var kalitlar = sotuvGuruhlari.Select(x => x.Key).Union(harakatGuruhlari.Select(x => x.Key)).ToList();

            // Tartib: operator — ism bo'yicha, kun/oy — yangisi tepada.
            kalitlar = g2 == HisobotGuruhi.Operator
                ? kalitlar.OrderBy(k => ismlar.GetValueOrDefault(int.Parse(k), "?")).ToList()
                : kalitlar.OrderByDescending(k => k).ToList();

            var qatorlar = new List<HisobotQatoriDto>();
            foreach (var k in kalitlar)
            {
                var nom = GuruhNomi(k);
                foreach (var y in sotuvGuruhlari[k].GroupBy(YoqilgiNomi).OrderBy(y => y.Key))
                    qatorlar.Add(Qator(nom, y.Key, y, []));
                qatorlar.Add(Qator(nom, null, sotuvGuruhlari[k], harakatGuruhlari[k]));
            }
            return new HisobotDto(qatorlar.ToArray(), Qator("Jami", null, sotuvlar, harakatlar));
        }).RuxsatKerak(Ruxsat.Hisobotlar).RequireAuthorization();

        app.MapGet("/boshqaruv/bugun", async (FuelControlDbContext db) =>
        {
            var hozir = DateTime.UtcNow;
            var bugun = Vaqt.KunBoshi(hozir);
            var kecha = bugun.AddDays(-1);
            var mahalliy = hozir.Add(Vaqt.Toshkent);
            var oyBoshi = Vaqt.Dan(new DateOnly(mahalliy.Year, mahalliy.Month, 1))!.Value;
            var boshi = bugun.AddDays(-13);
            var dan = oyBoshi < boshi ? oyBoshi : boshi;

            var sotuvlar = await db.Sotuvlar.Include(x => x.Tolovlar).Where(x => x.Vaqt >= dan).ToListAsync();
            var faol = sotuvlar.Where(x => x.Faolmi).ToList();
            var bugunFaol = faol.Where(x => x.Vaqt >= bugun).ToList();
            var ochiq = await db.Smenalar.Where(x => x.Tugadi == null).ToListAsync();
            var aparatlar = await db.Aparatlar.ToDictionaryAsync(a => a.Id);
            var yoqilgilar = await db.Yoqilgilar.OrderBy(y => y.Id).ToListAsync();
            var oyKamomat = -(await db.Harakatlar.Where(h => h.Turi == HarakatTuri.Kamomat && h.Sana >= oyBoshi).Select(h => h.Summa).ToListAsync()).Sum();

            var kpi = new BoshqaruvKpiDto(
                bugunFaol.Sum(x => x.Summa), bugunFaol.Sum(x => x.Litr), bugunFaol.Count, ochiq.Count,
                faol.Where(x => x.Vaqt >= kecha && x.Vaqt < bugun).Sum(x => x.Summa),
                faol.Where(x => x.Vaqt >= oyBoshi).Sum(x => x.Summa),
                oyKamomat);
            var tolov = Enum.GetValues<TolovTuri>()
                .Select(t => new TolovUlushiDto(t, bugunFaol.Sum(x => SmenaHisoblagich.ToloviYigindisi(x, t)))).ToArray();
            var yoqilgi = yoqilgilar.Select(y =>
            {
                var q = bugunFaol.Where(x => aparatlar[x.AparatId].YoqilgiTuriId == y.Id).ToList();
                return new YoqilgiUlushiDto(y.Nomi, y.Rang, q.Sum(x => x.Litr), q.Sum(x => x.Summa));
            }).ToArray();
            var kunlar = Enumerable.Range(0, 14).Select(i =>
            {
                var k = boshi.AddDays(i);
                var qism = faol.Where(x => x.Vaqt >= k && x.Vaqt < k.AddDays(1)).ToList();
                return new KunlikDto(DateOnly.FromDateTime(k.Add(Vaqt.Toshkent)), qism.Sum(x => x.Summa), qism.Sum(x => x.Litr));
            }).ToArray();
            var operatorlar = await db.Foydalanuvchilar.Where(f => f.Faol && f.Rol == Rol.Operator).ToListAsync();
            var opQisqa = operatorlar.Select(f =>
            {
                var q = bugunFaol.Where(x => x.OperatorId == f.Id).ToList();
                return new OperatorQisqaDto(f.Id, f.ToliqIsm, q.Sum(x => x.Summa), q.Sum(x => x.Litr), q.Count, ochiq.Any(s => s.OperatorId == f.Id));
            }).OrderByDescending(x => x.SotuvSoni).ToArray();
            var oxirgi = await db.Sotuvlar.Include(x => x.Tolovlar).OrderByDescending(x => x.Vaqt).Take(8).ToListAsync();
            return new BoshqaruvBugunDto(kpi, tolov, yoqilgi, kunlar, opQisqa, (await db.SotuvDtolari(oxirgi)).ToArray());
        }).RuxsatKerak(Ruxsat.Boshqaruv).RequireAuthorization();

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

            var mahalliy = DateTime.UtcNow.Add(Vaqt.Toshkent);
            var joriyOy = Vaqt.Dan(new DateOnly(mahalliy.Year, mahalliy.Month, 1))!.Value;
            var oySavdo = (await db.Sotuvlar.Where(s => s.OperatorId == id && s.Holati == SotuvHolati.Faol && s.Vaqt >= joriyOy)
                .Select(s => s.Summa).ToListAsync()).Sum();
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
            Audit.Yoz(db, ctx.User.Ism(), s.Turi == HarakatTuri.Avans ? "Avans berildi" : "To'lov berildi", $"{f.ToliqIsm}: {s.Summa} so'm. {s.Izoh}");
            await db.SaveChangesAsync();
            await hub.Bildir(Bolimlar.Harakatlar);
            return Results.Created($"/operatorlar/{id}/hisob", h.Dto());
        }).RuxsatKerak(Ruxsat.AvansBerish).Produces<HisobHarakatiDto>(201);

        app.MapGet("/audit", async (FuelControlDbContext db, string? q, DateOnly? dan, DateOnly? gacha, int? limit) =>
        {
            var aq = db.Audit.AsQueryable();
            if (Vaqt.Dan(dan) is { } d) aq = aq.Where(x => x.Vaqt >= d);
            if (Vaqt.Gacha(gacha) is { } g) aq = aq.Where(x => x.Vaqt < g);
            if (!string.IsNullOrWhiteSpace(q)) aq = aq.Where(x => x.Kim.Contains(q) || x.Amal.Contains(q) || x.Tafsilot.Contains(q));
            return (await aq.OrderByDescending(x => x.Vaqt).ThenByDescending(x => x.Id).Take(Math.Clamp(limit ?? 1000, 1, 5000)).ToListAsync())
                .Select(x => new AuditYozuviDto(x.Id, x.Vaqt, x.Kim, x.Amal, x.Tafsilot));
        }).RuxsatKerak(Ruxsat.Audit).RequireAuthorization();

        // Eksport klientda bajariladi (Excel fayl foydalanuvchi kompyuterida) — server faqat auditga yozadi.
        app.MapPost("/audit/eksport", async (AuditEksportDto s, HttpContext ctx, FuelControlDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(s.Turi)) throw new BiznesXatosi("Eksport turi kiritilishi shart.");
            Audit.Yoz(db, ctx.User.Ism(), $"Eksport: {s.Turi.Trim()}", s.Tafsilot ?? "");
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RuxsatKerak(Ruxsat.Eksport).RequireAuthorization().Produces(204);
    }
}
