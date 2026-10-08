using System.Globalization;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

/// <summary>Hisobot (faqat yopilgan smenalar) va Boshqaruv yig'indilari: sof mantiq, ma'lumotni Api yuklaydi.</summary>
public static class HisobotXizmati
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>NaqdSavdo = Savdo - Plastik - Depozit(farqi) - Nasiya. Kamomat/Ortiqcha - smenalar bo'yicha alohida yig'indi (musbat sonlar).</summary>
    public static HisobotQatoriDto Qator(string guruh, DateOnly? sana, string? operatorIsmi, IReadOnlyCollection<Smena> smenalar, bool jami = false,
        IReadOnlyDictionary<int, int>? xarajatSonlari = null)
    {
        var savdo = smenalar.Sum(s => s.Savdo);
        var plastik = smenalar.Sum(s => s.Plastik);
        var depozit = smenalar.Sum(s => s.DepozitFarqi);
        var nasiya = smenalar.Sum(s => s.NasiyaJami);
        return new HisobotQatoriDto(guruh, sana, operatorIsmi, smenalar.Count, smenalar.Sum(s => s.JamiLitr),
            savdo, plastik, depozit, nasiya, smenalar.Sum(s => s.QaytganNasiya), smenalar.Sum(s => s.XarajatJami),
            savdo - plastik - depozit - nasiya,
            smenalar.Where(s => s.Farq < 0).Sum(s => -s.Farq), smenalar.Where(s => s.Farq > 0).Sum(s => s.Farq), jami,
            smenalar.Sum(s => xarajatSonlari?.GetValueOrDefault(s.Id) ?? 0));
    }

    /// <summary>
    /// Guruh qiymati tilga bog'liq emas: smena "41", kun "yyyy-MM-dd", oy "yyyy-MM", operator - ism. Kunlik/oylik guruhlashda smena
    /// ochilgan Toshkent sanasiga yoziladi. Tartib: operator - ism bo'yicha, qolganlari - yangisi tepada.
    /// </summary>
    public static HisobotQatoriDto[] Qatorlar(HisobotGuruhi guruh, IReadOnlyCollection<Smena> smenalar, IReadOnlyDictionary<int, string> ismlar,
        IReadOnlyDictionary<int, int>? xarajatSonlari = null)
    {
        string Ism(int id) => ismlar.GetValueOrDefault(id, "?");
        return guruh switch
        {
            HisobotGuruhi.Smena => smenalar.OrderByDescending(s => s.Boshlandi).ThenByDescending(s => s.Id)
                .Select(s => Qator(s.Id.ToString(Inv), Vaqt.Sana(s.Boshlandi), Ism(s.OperatorId), [s], false, xarajatSonlari)).ToArray(),
            HisobotGuruhi.Kun => smenalar.GroupBy(s => Vaqt.Sana(s.Boshlandi)).OrderByDescending(g => g.Key)
                .Select(g => Qator(g.Key.ToString("yyyy-MM-dd", Inv), g.Key, null, g.ToList(), false, xarajatSonlari)).ToArray(),
            HisobotGuruhi.Oy => smenalar.GroupBy(s => Vaqt.Sana(s.Boshlandi).ToString("yyyy-MM", Inv)).OrderByDescending(g => g.Key)
                .Select(g => Qator(g.Key, null, null, g.ToList(), false, xarajatSonlari)).ToArray(),
            HisobotGuruhi.Operator => smenalar.GroupBy(s => s.OperatorId).OrderBy(g => Ism(g.Key)).ThenBy(g => g.Key)
                .Select(g => Qator(Ism(g.Key), null, null, g.ToList(), false, xarajatSonlari)).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(guruh)),
        };
    }

    /// <summary>
    /// Aparat/bak jadvali (litrda). Sotildi/Savdo - davrdagi (Boshlandi dan..gacha) yopilgan smenalar; Kirim - davrdagi bak kirimlari.
    /// BakOxirida - davr oxiridagi qoldiq: hozirgidan davrdan keyingi kirim/tuzatish ayriladi, davrdan keyin yopilgan smenalarda
    /// sotilgan qo'shiladi. BakBoshida = BakOxirida - Kirim + Sotildi (davr ichidagi qo'lda tuzatish BakBoshida'ga singadi).
    /// yopilganlar - dan'dan boshlab hamma yopilgan smenalar (davrdan keyingilari ham).
    /// </summary>
    public static HisobotAparatDto[] Aparatlar(IReadOnlyCollection<Aparat> aparatlar, IReadOnlyDictionary<int, string> yoqilgiNomlari,
        IReadOnlyCollection<Smena> yopilganlar, IReadOnlyCollection<SmenaKorsatkichi> segmentlar, IReadOnlyCollection<BakKirim> kirimlar,
        IReadOnlyCollection<BakTuzatishi> tuzatishlar, DateTime? danUtc, DateTime? gachaUtc)
    {
        bool Davrda(DateTime t) => (danUtc is null || t >= danUtc) && (gachaUtc is null || t < gachaUtc);
        bool Keyin(DateTime t) => gachaUtc is not null && t >= gachaUtc;
        var davr = yopilganlar.Where(s => Davrda(s.Boshlandi)).Select(s => s.Id).ToHashSet();
        var keyin = yopilganlar.Where(s => Keyin(s.Boshlandi)).Select(s => s.Id).ToHashSet();

        return aparatlar.OrderBy(a => a.Raqam).Select(a =>
        {
            var seg = segmentlar.Where(x => x.AparatId == a.Id).ToList();
            var sotildi = seg.Where(x => davr.Contains(x.SmenaId)).Sum(x => x.Litr);
            var savdo = seg.Where(x => davr.Contains(x.SmenaId)).Sum(x => x.Summa);
            var kirim = kirimlar.Where(k => k.AparatId == a.Id && Davrda(k.Vaqt)).Sum(k => k.Litr);
            var oxirida = a.BakQoldiq
                - kirimlar.Where(k => k.AparatId == a.Id && Keyin(k.Vaqt)).Sum(k => k.Litr)
                + seg.Where(x => keyin.Contains(x.SmenaId)).Sum(x => x.Litr)
                - tuzatishlar.Where(t => t.AparatId == a.Id && Keyin(t.Vaqt)).Sum(t => t.Keyin - t.Oldin);
            return new HisobotAparatDto(a.Id, a.Raqam, yoqilgiNomlari.GetValueOrDefault(a.YoqilgiTuriId, "?"),
                oxirida - kirim + sotildi, kirim, sotildi, oxirida, savdo);
        }).ToArray();
    }

    public static HisobotDto Tuz(HisobotGuruhi guruh, IReadOnlyCollection<Smena> qatorSmenalari, IReadOnlyDictionary<int, string> ismlar,
        HisobotAparatDto[] aparatlar, long avans, IReadOnlyDictionary<int, int>? xarajatSonlari = null) =>
        new(Qatorlar(guruh, qatorSmenalari, ismlar, xarajatSonlari), Qator("Jami", null, null, qatorSmenalari, true, xarajatSonlari), aparatlar, avans);
}

public static class BoshqaruvXizmati
{
    public sealed record OyKorsatkichlari(long Savdo, decimal Litr, int SmenaSoni, long Kamomat, long Ortiqcha, TolovTaqsimotiDto Tolovlar);

    /// <summary>Joriy (Toshkent) oyda ochilgan yopilgan smenalar bo'yicha. Naqd = Savdo - Plastik - Depozit(farqi) - Nasiya.</summary>
    public static OyKorsatkichlari Oy(IReadOnlyCollection<Smena> oyYopilganlari)
    {
        var savdo = oyYopilganlari.Sum(s => s.Savdo);
        var plastik = oyYopilganlari.Sum(s => s.Plastik);
        var depozit = oyYopilganlari.Sum(s => s.DepozitFarqi);
        var nasiya = oyYopilganlari.Sum(s => s.NasiyaJami);
        return new OyKorsatkichlari(savdo, oyYopilganlari.Sum(s => s.JamiLitr), oyYopilganlari.Count,
            oyYopilganlari.Where(s => s.Farq < 0).Sum(s => -s.Farq), oyYopilganlari.Where(s => s.Farq > 0).Sum(s => s.Farq),
            new TolovTaqsimotiDto(savdo - plastik - depozit - nasiya, plastik, depozit, nasiya));
    }

    /// <summary>Oxirgi yopilgan smenalar (grafik uchun), eskisidan yangisiga.</summary>
    public static SmenaQisqaDto[] OxirgiSmenalar(IEnumerable<Smena> yopilganlar, IReadOnlyDictionary<int, string> ismlar, int soni = 14) =>
        yopilganlar.OrderByDescending(s => s.Boshlandi).ThenByDescending(s => s.Id).Take(soni).Reverse()
            .Select(s => new SmenaQisqaDto(s.Id, Vaqt.Sana(s.Boshlandi), ismlar.GetValueOrDefault(s.OperatorId, "?"), s.Savdo, s.JamiLitr, s.Farq))
            .ToArray();
}
