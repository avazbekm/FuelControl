using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

/// <summary>Core modellarini shartnoma DTO'lariga aylantirish va ularni bazadan yig'ish (smena, nasiya, xarajat, aparat, bak).</summary>
public static class Dtolar
{
    /// <summary>
    /// Ochiq smenada JamiLitr/Savdo/Plastik/DepozitFarqi/Kutilgan/Farq = 0, NasiyaJami/QaytganNasiya/XarajatJami - joriy yig'indilar (jonli);
    /// yopilganda - saqlangan natija.
    /// </summary>
    public static SmenaDto Dto(this Smena s, string operatorIsmi, SmenaHisoblagich.Yigindilar? jonli = null)
    {
        if (!s.Ochiqmi)
            return new SmenaDto(s.Id, s.OperatorId, operatorIsmi, s.Boshlandi, s.Tugadi, s.OchishQaytim, s.OchishTerminal, s.OchishDepozit,
                s.YopishTerminal, s.YopishDepozit, s.SanalganNaqd, s.JamiLitr, s.Savdo, s.Plastik, s.DepozitFarqi,
                s.NasiyaJami, s.QaytganNasiya, s.XarajatJami, s.Kutilgan, s.Farq, s.Izoh, s.PlastikSummalari);
        var y = jonli ?? SmenaHisoblagich.Yigindilar.Bosh;
        return new SmenaDto(s.Id, s.OperatorId, operatorIsmi, s.Boshlandi, null, s.OchishQaytim, s.OchishTerminal, s.OchishDepozit,
            null, null, null, 0, 0, 0, 0, y.NasiyaJami, y.QaytganNasiya, y.XarajatJami, 0, 0, s.Izoh, []);
    }

    /// <summary>
    /// Yopilgan smenaning pul natijalari yashirilgan nusxasi (SignalR: keyingi operator va boshqa kuzatuvchilarga, docs 8.10). Id, OperatorId,
    /// OperatorIsmi, Boshlandi va Tugadi qoladi; qolgani - ochish qoldiqlari, savdo, litr, plastik (+ qismlari), depozit, nasiya, qaytgan, xarajat,
    /// kutilgan, farq - 0, yopish terminal/depozit va sanalgan naqd - null, izoh - null, PlastikSummalari - bo'sh.
    /// </summary>
    public static SmenaDto PulSiz(this SmenaDto d) =>
        new(d.Id, d.OperatorId, d.OperatorIsmi, d.Boshlandi, d.Tugadi, 0, 0, 0, null, null, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, []);

    public static NasiyaDto Dto(this Nasiya n, DateOnly bugun) => new(n.Id, n.SmenaId, n.KimYozdi, n.MijozIsmi, n.Telefon, n.MashinaRaqami,
        n.Summa, n.Qaytgan, n.Qoldiq, n.Muddat, NasiyaXizmati.Holat(n, bugun), NasiyaXizmati.MuddatgachaKun(n, bugun), n.Yozildi, n.Yopildi, n.Izoh, n.OperatorId);

    public static NasiyaQaytishiDto Dto(this NasiyaQaytishi q, string mijozIsmi) =>
        new(q.Id, q.NasiyaId, mijozIsmi, q.SmenaId, q.Summa, q.Usul, q.Vaqt, q.KimYozdi, q.Izoh, q.OperatorId);

    public static XarajatDto Dto(this Xarajat x) => new(x.Id, x.SmenaId, x.Summa, x.Sabab, x.Manba, x.Vaqt, x.KimYozdi, x.OperatorId);

    public static BakKirimDto Dto(this BakKirim k, int aparatRaqami) =>
        new(k.Id, k.AparatId, aparatRaqami, k.Litr, k.QoldiqOldin, k.QoldiqKeyin, k.Vaqt, k.Hujjat, k.KimYozdi);

    public static AparatDto Dto(this Aparat a, string yoqilgiNomi, BakKirim? oxirgiKirim) =>
        new(a.Id, a.Raqam, a.YoqilgiTuriId, yoqilgiNomi, a.TotalLitr, a.BakQoldiq, oxirgiKirim?.Vaqt, oxirgiKirim?.Litr);

    public static SmenaKorsatkichDto Dto(this SmenaKorsatkichi k, Aparat a, string yoqilgiNomi) =>
        new(k.AparatId, a.Raqam, yoqilgiNomi, k.Boshi, k.Oxiri, k.Narx, k.Litr, k.Summa, k.NarxOzgarishida);

    public static YoqilgiTuriDto Dto(this YoqilgiTuri y, bool aparatgaBiriktirilgan) => new(y.Id, y.Nomi, y.Narx, y.Rang, aparatgaBiriktirilgan);

    public static Task<Dictionary<int, string>> OperatorIsmlari(this FuelControlDbContext db) =>
        db.Foydalanuvchilar.AsNoTracking().ToDictionaryAsync(f => f.Id, f => f.ToliqIsm);

    /// <summary>Smenaga yozilgan nasiya, smena hisobiga yozilgan qaytishlar va xarajatlar yig'indisi.</summary>
    public static async Task<SmenaHisoblagich.Yigindilar> Yigindilar(this FuelControlDbContext db, int smenaId) => new(
        await db.Nasiyalar.Where(n => n.SmenaId == smenaId).SumAsync(n => n.Summa),
        await db.NasiyaQaytishlari.Where(q => q.SmenaId == smenaId).SumAsync(q => q.Summa),
        await db.Xarajatlar.Where(x => x.SmenaId == smenaId).SumAsync(x => x.Summa));

    public static async Task<List<SmenaDto>> SmenaDtolari(this FuelControlDbContext db, IReadOnlyCollection<Smena> smenalar)
    {
        var ismlar = await db.OperatorIsmlari();
        var natija = new List<SmenaDto>(smenalar.Count);
        foreach (var s in smenalar)
            natija.Add(s.Dto(ismlar.GetValueOrDefault(s.OperatorId, "?"), s.Ochiqmi ? await db.Yigindilar(s.Id) : null));
        return natija;
    }

    public static async Task<SmenaDto> SmenaDtosi(this FuelControlDbContext db, Smena s) => (await db.SmenaDtolari([s]))[0];

    /// <summary>Aparatlar (Raqam bo'yicha) oxirgi bak kirimi bilan.</summary>
    public static async Task<List<AparatDto>> AparatDtolari(this FuelControlDbContext db)
    {
        var nomlar = await db.Yoqilgilar.AsNoTracking().ToDictionaryAsync(y => y.Id, y => y.Nomi);
        var oxirgi = (await db.BakKirimlari.AsNoTracking().OrderByDescending(k => k.Vaqt).ThenByDescending(k => k.Id).ToListAsync())
            .GroupBy(k => k.AparatId).ToDictionary(g => g.Key, g => g.First());
        return (await db.Aparatlar.AsNoTracking().OrderBy(a => a.Raqam).ToListAsync())
            .Select(a => a.Dto(nomlar.GetValueOrDefault(a.YoqilgiTuriId, "?"), oxirgi.GetValueOrDefault(a.Id))).ToList();
    }

    public static async Task<AparatDto> AparatDtosi(this FuelControlDbContext db, Aparat a)
    {
        var nom = await db.Yoqilgilar.Where(y => y.Id == a.YoqilgiTuriId).Select(y => y.Nomi).FirstOrDefaultAsync() ?? "?";
        var oxirgi = await db.BakKirimlari.AsNoTracking().Where(k => k.AparatId == a.Id)
            .OrderByDescending(k => k.Vaqt).ThenByDescending(k => k.Id).FirstOrDefaultAsync();
        return a.Dto(nom, oxirgi);
    }

    /// <summary>Smena tafsiloti: segmentlar (ochiq smenada faqat narx o'zgarishida qayd etilganlar), smenaning nasiya/qaytish/xarajatlari - vaqt bo'yicha.</summary>
    public static async Task<SmenaTafsilotDto> SmenaTafsiloti(this FuelControlDbContext db, Smena smena)
    {
        var bugun = Vaqt.Sana(DateTime.UtcNow);
        var aparatlar = await db.Aparatlar.AsNoTracking().ToDictionaryAsync(a => a.Id);
        var yoqilgilar = await db.Yoqilgilar.AsNoTracking().ToDictionaryAsync(y => y.Id, y => y.Nomi);
        var segmentlar = (await db.SmenaKorsatkichlari.AsNoTracking().Where(x => x.SmenaId == smena.Id).ToListAsync())
            .OrderBy(x => aparatlar[x.AparatId].Raqam).ThenBy(x => x.Tartib)
            .Select(x => x.Dto(aparatlar[x.AparatId], yoqilgilar.GetValueOrDefault(aparatlar[x.AparatId].YoqilgiTuriId, "?"))).ToArray();
        var nasiyalar = await db.Nasiyalar.AsNoTracking().Where(n => n.SmenaId == smena.Id).OrderBy(n => n.Yozildi).ThenBy(n => n.Id).ToListAsync();
        var qaytishlar = await db.NasiyaQaytishlari.AsNoTracking().Where(q => q.SmenaId == smena.Id).OrderBy(q => q.Vaqt).ThenBy(q => q.Id).ToListAsync();
        var mijozlar = await db.Nasiyalar.AsNoTracking().Where(n => qaytishlar.Select(q => q.NasiyaId).Contains(n.Id)).ToDictionaryAsync(n => n.Id, n => n.MijozIsmi);
        var xarajatlar = await db.Xarajatlar.AsNoTracking().Where(x => x.SmenaId == smena.Id).OrderBy(x => x.Vaqt).ThenBy(x => x.Id).ToListAsync();
        return new SmenaTafsilotDto(await db.SmenaDtosi(smena), segmentlar,
            nasiyalar.Select(n => n.Dto(bugun)).ToArray(),
            qaytishlar.Select(q => q.Dto(mijozlar.GetValueOrDefault(q.NasiyaId, "?"))).ToArray(),
            xarajatlar.Select(x => x.Dto()).ToArray());
    }
}
