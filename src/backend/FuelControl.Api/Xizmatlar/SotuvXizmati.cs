using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

/// <summary>Sotuv yaratish/tahrirlash/bekor qilish: totalizator va smena yakunlarini bir tranzaksiyada yangilaydi.</summary>
public static class SotuvXizmati
{
    public static async Task SmenaniQaytaHisobla(FuelControlDbContext db, Smena smena)
    {
        var faol = await db.Sotuvlar.Include(x => x.Tolovlar)
            .Where(x => x.SmenaId == smena.Id && x.Holati == SotuvHolati.Faol).ToListAsync();
        SmenaHisoblagich.QaytaHisobla(smena, faol);
    }

    private static List<SotuvTolovi> Tolovlar(TolovDto[] tolovlar, long summa)
    {
        if (tolovlar is null || tolovlar.Length == 0) throw new BiznesXatosi("To'lov turi kiritilishi shart.");
        if (tolovlar.Any(t => t.Summa <= 0)) throw new BiznesXatosi("To'lov summalari musbat bo'lishi kerak.");
        if (tolovlar.Sum(t => t.Summa) != summa) throw new BiznesXatosi("To'lovlar yig'indisi sotuv summasiga teng bo'lishi kerak.");
        return tolovlar.Select(t => new SotuvTolovi { Turi = t.Turi, Summa = t.Summa }).ToList();
    }

    /// <summary>Yangi smena ochadi. Parallel so'rov shu operatorga smenani oldinroq ochgan bo'lsa
    /// (IX_Smenalar_OchiqSmena buziladi) — o'sha ochiq smena qaytadi, Yangi = false.</summary>
    public static async Task<(Smena Smena, bool Yangi)> SmenaOch(FuelControlDbContext db, int operatorId, string izoh)
    {
        var smena = new Smena { OperatorId = operatorId, Boshlandi = DateTime.UtcNow };
        db.Smenalar.Add(smena);
        var ism = await db.Foydalanuvchilar.Where(f => f.Id == operatorId).Select(f => f.ToliqIsm).FirstAsync();
        Audit.Yoz(db, ism, "Smena ochildi", izoh);
        try
        {
            await db.SaveChangesAsync();
            return (smena, true);
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // Faqat shu yerda qo'shilgan (Added) yozuvlarni tashlaymiz — oldin yuklangan aparat va h.k. kuzatuvda qoladi.
            foreach (var y in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added).ToList())
                y.State = EntityState.Detached;
            var mavjud = await db.Smenalar.FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.Tugadi == null);
            if (mavjud is null) throw;
            return (mavjud, false);
        }
    }

    /// <returns>Yangi = false — shu IdempotencyKey bilan sotuv parallel so'rovda allaqachon yozilgan.</returns>
    public static async Task<(Sotuv Sotuv, bool Yangi)> Yarat(FuelControlDbContext db, int operatorId, SotuvYaratishDto s)
    {
        if ((s.Litr is null) == (s.Summa is null)) throw new BiznesXatosi("Litr yoki summadan faqat bittasini kiriting.");
        if (s.Litr <= 0 || s.Summa <= 0) throw new BiznesXatosi("Litr va summa musbat bo'lishi kerak.");

        var aparat = await db.Aparatlar.FindAsync(s.AparatId) ?? throw new BiznesXatosi("Aparat topilmadi.", 404);
        var yoqilgi = await db.Yoqilgilar.FindAsync(aparat.YoqilgiTuriId)!;
        var narx = yoqilgi!.Narx;

        decimal litr; long summa;
        if (s.Summa is { } kiritilganSumma) { summa = kiritilganSumma; litr = SotuvHisoblagich.SummadanLitr(summa, narx); }
        else { litr = Math.Round(s.Litr!.Value, 2, MidpointRounding.AwayFromZero); summa = SotuvHisoblagich.LitrdanSumma(litr, narx); }

        var aralash = s.Tolovlar is { Length: > 0 };
        if (aralash == (s.TolovTuri is not null))
            throw new BiznesXatosi("To'lov turini (TolovTuri) yoki to'lovlar ro'yxatini (Tolovlar) — faqat bittasini bering.");
        var tolovlar = aralash
            ? Tolovlar(s.Tolovlar!, summa)
            : [new SotuvTolovi { Turi = s.TolovTuri!.Value, Summa = summa }];

        // SmenaOchish ruxsati ataylab tekshirilmaydi: TZ bo'yicha smena birinchi sotuvda o'zi ochiladi.
        var smena = await db.Smenalar.FirstOrDefaultAsync(x => x.OperatorId == operatorId && x.Tugadi == null)
            ?? (await SmenaOch(db, operatorId, "Birinchi sotuvda avtomatik")).Smena;

        var sotuv = new Sotuv
        {
            SmenaId = smena.Id, OperatorId = operatorId, AparatId = aparat.Id, Narx = narx, Litr = litr, Summa = summa,
            Vaqt = DateTime.UtcNow, Tolovlar = tolovlar, IdempotencyKey = s.IdempotencyKey,
        };
        db.Sotuvlar.Add(sotuv);
        aparat.TotalLitr += litr;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // Parallel so'rov shu kalitni oldinroq yozgan: SaveChanges tranzaksiyasi to'liq bekor bo'ldi
            // (totalizator ham), kuzatuvdagi o'zgarishlarni tashlab, mavjud sotuvni qaytaramiz.
            db.ChangeTracker.Clear();
            var mavjud = await db.Sotuvlar.Include(x => x.Tolovlar).FirstOrDefaultAsync(x => x.IdempotencyKey == s.IdempotencyKey);
            if (mavjud is null) throw;
            return (mavjud, false);
        }
        await SmenaniQaytaHisobla(db, smena);
        await db.SaveChangesAsync();
        return (sotuv, true);
    }

    public static async Task<Sotuv> Tahrirla(FuelControlDbContext db, int id, SotuvTahrirlashDto s, string kim)
    {
        if (string.IsNullOrWhiteSpace(s.Sabab)) throw new BiznesXatosi("Tahrirlash sababi majburiy.");
        if (s.Summa <= 0) throw new BiznesXatosi("Summa musbat bo'lishi kerak.");

        var sotuv = await db.Sotuvlar.Include(x => x.Tolovlar).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BiznesXatosi("Sotuv topilmadi.", 404);
        if (!sotuv.Faolmi) throw new BiznesXatosi("Bekor qilingan sotuvni tahrirlash mumkin emas.", 409);

        var eskiAparat = await db.Aparatlar.FindAsync(sotuv.AparatId);
        var yangiAparat = await db.Aparatlar.FindAsync(s.AparatId) ?? throw new BiznesXatosi("Aparat topilmadi.", 404);
        var narx = yangiAparat.YoqilgiTuriId == eskiAparat!.YoqilgiTuriId
            ? sotuv.Narx
            : (await db.Yoqilgilar.FindAsync(yangiAparat.YoqilgiTuriId))!.Narx;

        var smena = (await db.Smenalar.FindAsync(sotuv.SmenaId))!;
        var eskiFarq = SmenaHisoblagich.Farq(smena);
        var eskiTolov = string.Join('+', sotuv.Tolovlar.Select(t => $"{t.Turi} {t.Summa}"));

        var natija = SotuvTahrirXizmati.Tahrirla(sotuv, s.AparatId, narx, s.Summa, Tolovlar(s.Tolovlar, s.Summa));

        eskiAparat.TotalLitr -= natija.EskiLitr;
        yangiAparat.TotalLitr += natija.YangiLitr;
        await db.SaveChangesAsync();
        await SmenaniQaytaHisobla(db, smena);
        TuzatishYoz(db, smena, eskiFarq, kim, $"Sotuv #{sotuv.Id} tahrirlandi");

        Audit.Yoz(db, kim, "Sotuv tahrirlandi",
            $"#{sotuv.Id}: aparat #{eskiAparat.Raqam}→#{yangiAparat.Raqam}, summa {natija.EskiSumma}→{natija.YangiSumma}, " +
            $"litr {natija.EskiLitr}→{natija.YangiLitr}, to'lov [{eskiTolov}]→[{string.Join('+', sotuv.Tolovlar.Select(t => $"{t.Turi} {t.Summa}"))}]. Sabab: {s.Sabab}");
        await db.SaveChangesAsync();
        return sotuv;
    }

    public static async Task<Sotuv> BekorQil(FuelControlDbContext db, int id, string sabab, string kim)
    {
        if (string.IsNullOrWhiteSpace(sabab)) throw new BiznesXatosi("Bekor qilish sababi majburiy.");
        var sotuv = await db.Sotuvlar.Include(x => x.Tolovlar).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new BiznesXatosi("Sotuv topilmadi.", 404);
        if (!sotuv.Faolmi) throw new BiznesXatosi("Sotuv allaqachon bekor qilingan.", 409);

        var aparat = (await db.Aparatlar.FindAsync(sotuv.AparatId))!;
        var smena = (await db.Smenalar.FindAsync(sotuv.SmenaId))!;
        var eskiFarq = SmenaHisoblagich.Farq(smena);

        SotuvTahrirXizmati.BekorQil(sotuv, sabab, kim);
        aparat.TotalLitr -= sotuv.Litr;
        await db.SaveChangesAsync();
        await SmenaniQaytaHisobla(db, smena);
        TuzatishYoz(db, smena, eskiFarq, kim, $"Sotuv #{sotuv.Id} bekor qilindi");

        Audit.Yoz(db, kim, "Sotuv bekor qilindi", $"#{sotuv.Id} {sotuv.Summa} so'm, {sotuv.Litr} L. Sabab: {sabab}");
        await db.SaveChangesAsync();
        return sotuv;
    }

    /// <summary>Yopilgan smena sotuvi o'zgarsa, farq o'zgarishi operator hisobiga tuzatish harakati sifatida yoziladi.</summary>
    private static void TuzatishYoz(FuelControlDbContext db, Smena smena, long eskiFarq, string kim, string izoh)
    {
        if (smena.Ochiqmi) return;
        var delta = SmenaHisoblagich.Farq(smena) - eskiFarq;
        if (delta == 0) return;
        db.Harakatlar.Add(new HisobHarakati
        {
            OperatorId = smena.OperatorId, Sana = DateTime.UtcNow,
            Turi = delta < 0 ? HarakatTuri.Kamomat : HarakatTuri.Ortiqcha,
            Summa = delta, Izoh = $"Smena #{smena.Id} tuzatish: {izoh}", KimYozdi = kim,
        });
    }
}
