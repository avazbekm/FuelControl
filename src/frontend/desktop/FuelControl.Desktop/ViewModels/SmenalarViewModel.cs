using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Smena tafsilotidagi aparat segmenti.</summary>
public sealed record KorsatkichQatori(SmenaKorsatkichDto K)
{
    public string Aparat => $"{K.AparatRaqam} · {K.YoqilgiNomi}";
    public string Boshi => Format.Son(K.Boshi);
    public string Oxiri => Format.Son(K.Oxiri);
    public string Litr => Format.Son(K.Litr);
    public string Summa => Format.Pul(K.Summa);
    /// <summary>Narx o'zgarishida qayd etilgan (eski narxdagi) segment — narxi izohda.</summary>
    public string? Izoh => K.NarxOzgarishida ? $"{Til.T("Smenalar_Narx")} {Format.Pul(K.Narx)}" : null;
    public bool IzohBor => K.NarxOzgarishida;
}

/// <summary>Smenalar ro'yxati qatori (tanlov belgisi bilan).</summary>
public partial class SmenaElementi(Smena s) : ObservableObject
{
    public Smena S { get; } = s;
    [ObservableProperty] private bool _tanlangan;
}

/// <summary>Pul hisobi satri: "+15 995 270" / "−7 330 000".</summary>
public sealed record PulSatri(string Nomi, string Qiymat, bool Qalin = false);

/// <summary>
/// Smenalar: davr (7 kun / shu oy / o'tgan oy) va operator filtri, ro'yxat (serverdan), tafsilot — aparat ko'rsatkichlari va pul hisobi.
/// Oxirgi yopilgan smenada "Ko'rsatkichni tuzatish" (KorsatkichTuzatish ruxsati, §1.11).
/// </summary>
public partial class SmenalarViewModel : ObservableObject
{
    public ObservableCollection<SmenaElementi> Smenalar { get; } = new();
    public List<Foydalanuvchi> OperatorFiltri { get; private set; } = new();

    [ObservableProperty] private Foydalanuvchi? _tanlanganOperator;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Davr7), nameof(DavrOy), nameof(DavrOtgan))]
    private int _davr = 1; // 0 — 7 kun, 1 — shu oy, 2 — o'tgan oy

    [ObservableProperty] private Smena? _tanlangan;
    [ObservableProperty] private SmenaTafsilotDto? _tafsilot;

    public bool Davr7 => Davr == 0;
    public bool DavrOy => Davr == 1;
    public bool DavrOtgan => Davr == 2;

    private readonly KechiktirilganIsh _royxat;
    private readonly KechiktirilganIsh _tafsilotYuklash;
    private static Foydalanuvchi Joriy => Malumot.JoriyFoydalanuvchi;

    public SmenalarViewModel()
    {
        _royxat = new KechiktirilganIsh(RoyxatniYukla, 250);
        _tafsilotYuklash = new KechiktirilganIsh(TafsilotniYukla, 150);
        OperatorFiltriniQur();
        Malumot.Ozgardi += () =>
        {
            if (!OperatorFiltri.Skip(1).Select(o => o.Id).SequenceEqual(Malumot.Operatorlar.Select(o => o.Id))) OperatorFiltriniQur();
            if (Malumot.Kirilgan && Joriy.Bor(Ruxsat.Smenalar)) _royxat.Rejala();
            else if (!Malumot.Kirilgan) { Smenalar.Clear(); Tanlangan = null; }
            OnPropertyChanged(string.Empty);
        };
        Til.Ozgardi += () => { OperatorFiltriniQur(); OnPropertyChanged(string.Empty); };
    }

    private void OperatorFiltriniQur()
    {
        var id = TanlanganOperator?.Id ?? 0;
        OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("Smenalar_BarchaOperatorlar") }, .. Malumot.Operatorlar];
        OnPropertyChanged(nameof(OperatorFiltri));
        TanlanganOperator = OperatorFiltri.FirstOrDefault(o => o.Id == id) ?? OperatorFiltri[0];
    }

    partial void OnDavrChanged(int value) { if (Malumot.Kirilgan) _royxat.Rejala(); }
    partial void OnTanlanganOperatorChanged(Foydalanuvchi? value) { if (Malumot.Kirilgan) _royxat.Rejala(); }
    partial void OnTanlanganChanged(Smena? value)
    {
        foreach (var e in Smenalar) e.Tanlangan = e.S == value;
        Tafsilot = null;
        if (value is not null && Malumot.Kirilgan) _tafsilotYuklash.Rejala();
    }
    partial void OnTafsilotChanged(SmenaTafsilotDto? value) => OnPropertyChanged(string.Empty);

    [RelayCommand] private void DavrTanla(string d) => Davr = int.Parse(d);
    [RelayCommand] private void Tanla(SmenaElementi e) => Tanlangan = e.S;

    private (DateOnly Dan, DateOnly Gacha) DavrOraligi()
    {
        var b = Malumot.Bugun;
        var oy = new DateOnly(b.Year, b.Month, 1);
        return Davr switch
        {
            0 => (b.AddDays(-6), b),
            2 => (oy.AddMonths(-1), oy.AddDays(-1)),
            _ => (oy, b),
        };
    }

    public Task HozirYukla() => _royxat.Bajar();

    private async Task RoyxatniYukla(Func<bool> dolzarb)
    {
        var (dan, gacha) = DavrOraligi();
        int? op = TanlanganOperator is { Id: > 0 } o ? o.Id : null;
        var l = (await Malumot.Api.Smenalar(dan, gacha, op))!;
        if (!dolzarb()) return;
        var tid = Tanlangan?.Id;
        Smenalar.Clear();
        foreach (var d in l.OrderByDescending(x => x.Boshlandi)) Smenalar.Add(new SmenaElementi(Malumot.SmenaKorinishi(d)));
        var yangi = Smenalar.Select(e => e.S).FirstOrDefault(s => s.Id == tid) ?? Smenalar.Select(e => e.S).FirstOrDefault(s => !s.Ochiqmi) ?? Smenalar.FirstOrDefault()?.S;
        if (yangi != Tanlangan) Tanlangan = yangi;
        else
        {
            foreach (var e in Smenalar) e.Tanlangan = e.S == yangi;
            if (yangi is not null) _tafsilotYuklash.Rejala();
        }
        OnPropertyChanged(string.Empty);
    }

    private async Task TafsilotniYukla(Func<bool> dolzarb)
    {
        if (Tanlangan is not { } s) return;
        var t = await Malumot.Api.SmenaTafsiloti(s.Id);
        if (dolzarb() && Tanlangan == s) Tafsilot = t;
    }

    // ================= Sarlavha va KPI =================

    public string JoriyMatn => Malumot.JoriySmena is { } j
        ? Til.F("Smenalar_Joriy", j.Id, j.Operator.ToliqIsm, Format.QisqaSanaVaqt(j.Boshlandi))
        : Til.T("Smenalar_JoriyYoq");
    public string OchiqSoni => Malumot.JoriySmena is null ? "0" : "1";
    public string OchiqIzoh => Malumot.JoriySmena is { } j ? $"{j.Operator.ToliqIsm} · {j.Davomiylik}" : Til.T("Smenalar_OchiqYoq");
    public string SoniYorliq => Til.T(Davr switch { 0 => "Smenalar_KpiSoni7Kun", 2 => "Smenalar_KpiSoniOtganOy", _ => "Smenalar_KpiSoniShuOy" }).ToUpperInvariant();
    public string Soni => Smenalar.Count.ToString();
    public string SoniIzoh => Til.F("Smenalar_KpiSoniIzoh", Smenalar.Count(s => !s.S.Ochiqmi), Smenalar.Count(s => s.S.Ochiqmi));
    public string KamomatYorliq => Til.T(Davr switch { 0 => "Smenalar_KpiKamomat7Kun", 2 => "Smenalar_KpiKamomatOtganOy", _ => "Smenalar_KpiKamomatShuOy" }).ToUpperInvariant();
    public string Kamomat => Format.Pul(Smenalar.Sum(s => s.S.Kamomat));
    public string KamomatIzoh => Til.F("Smenalar_KpiKamomatIzoh", Format.Pul(Smenalar.Where(s => s.S.Farq > 0).Sum(s => s.S.Farq)));
    public string RoyxatSarlavha
    {
        get
        {
            if (Davr == 0) return Til.T("Smenalar_Royxat7Kun");
            var (dan, _) = DavrOraligi();
            var oy = Til.T("OyNomlari").Split(',')[dan.Month - 1];
            return Til.F("Smenalar_RoyxatOy", oy);
        }
    }
    public bool BoshRoyxat => Smenalar.Count == 0;

    // ================= Tafsilot =================

    private SmenaDto? D => Tafsilot?.Smena;
    public bool TafsilotBor => D is not null;
    public bool TafsilotYoq => D is null;
    public bool Ochiq => D is { Tugadi: null };
    public bool Yopilgan => D is { Tugadi: not null };
    public string TSarlavha => Til.F("Smenalar_Smena", D?.Id ?? 0);
    public string TIzoh => D is not { } d ? ""
        : d.Tugadi is { } t
            ? Til.F("Smenalar_DavomiYopilgan", d.OperatorIsmi, Format.QisqaSanaVaqt(d.Boshlandi.ToLocalTime()), Format.QisqaSanaVaqt(t.ToLocalTime()))
            : Til.F("Smenalar_DavomiOchiq", d.OperatorIsmi, Format.QisqaSanaVaqt(d.Boshlandi.ToLocalTime()));

    public List<KorsatkichQatori> Korsatkichlar => (Tafsilot?.Korsatkichlar ?? []).Select(k => new KorsatkichQatori(k)).ToList();
    public bool KorsatkichBor => Korsatkichlar.Count > 0;
    public string JamiLitr => Format.Son((Tafsilot?.Korsatkichlar ?? []).Sum(k => k.Litr));
    public string JamiSumma => Format.Pul((Tafsilot?.Korsatkichlar ?? []).Sum(k => k.Summa));

    public string OchQaytim => Format.Pul(D?.OchishQaytim ?? 0);
    public string OchTerminal => Format.Pul(D?.OchishTerminal ?? 0);
    public string OchDepozit => Format.Pul(D?.OchishDepozit ?? 0);

    private static string Plus(long n) => n == 0 ? "0" : (n > 0 ? "+" : "−") + Format.Pul(Math.Abs(n));
    private static string Minus(long n) => (n > 0 ? "−" : n < 0 ? "+" : "") + Format.Pul(Math.Abs(n));

    /// <summary>Kutilgan naqd tarkibi (§1.6): qaytim + savdo + qaytgan nasiya − plastik − depozit farqi − nasiya − xarajat.</summary>
    public List<PulSatri> PulHisobi
    {
        get
        {
            if (D is not { } d || Tafsilot is not { } t) return new();
            var l = new List<PulSatri> { new(Til.T("Smenalar_Qaytim"), Plus(d.OchishQaytim)) };
            if (d.Tugadi is not null)
            {
                l.Add(new(Til.T("Smenalar_SavdoAparatlar"), Plus(d.Savdo)));
                l.Add(new(Til.F("Smenalar_PlastikSatr", Format.Pul(d.YopishTerminal ?? 0), Format.Pul(d.OchishTerminal)), Minus(d.Plastik)));
                l.Add(new(Til.F("Smenalar_DepozitSatr", Format.Pul(d.OchishDepozit), Format.Pul(d.YopishDepozit ?? 0)), Minus(d.DepozitFarqi)));
            }
            foreach (var q in t.Qaytishlar) l.Add(new($"{Til.T("Smenalar_QaytganNasiya")} · {q.MijozIsmi}", Plus(q.Summa)));
            foreach (var n in t.Nasiyalar) l.Add(new(Til.F("Smenalar_NasiyaSatr", n.MijozIsmi), Minus(n.Summa)));
            foreach (var x in t.Xarajatlar)
                l.Add(new(Til.F("Smenalar_XarajatSatr", x.Sabab) + (x.Manba == XarajatManbai.Depozit ? $" ({Til.T("Smenalar_ManbaDepozit")})" : ""), Minus(x.Summa)));
            return l;
        }
    }

    public string Kutilgan => Format.Pul(D?.Kutilgan ?? 0);
    public string Sanalgan => D?.SanalganNaqd is { } s ? Format.Pul(s) : "—";
    public bool FarqYoq => Yopilgan && D!.Farq == 0;
    public bool KamomatBor => Yopilgan && D!.Farq < 0;
    public bool OrtiqchaBor => Yopilgan && D!.Farq > 0;
    public string FarqMatn => Format.Farq(D?.Farq ?? 0);
    public string FarqIzoh => D is not { } d ? "" : d.Farq < 0 ? Til.F("Smenalar_KamomatIzoh", d.OperatorIsmi) : Til.F("Smenalar_OrtiqchaIzoh", d.OperatorIsmi);
    public bool IzohBor => !string.IsNullOrWhiteSpace(D?.Izoh);

    /// <summary>Tuzatish faqat oxirgi yopilgan smena uchun (server ham tekshiradi).</summary>
    public bool TuzataOladi => Yopilgan && Joriy.Bor(Ruxsat.KorsatkichTuzatish) && Malumot.OxirgiYopilgan?.Id == D!.Id;
    public bool EksportOladi => TafsilotBor && Joriy.Bor(Ruxsat.Eksport);

    // ================= Tuzatish dialogi =================

    [ObservableProperty] private bool _tuzatishOchiq;
    [ObservableProperty] private SmenaKorsatkichDto? _tuzatishAparat;
    [ObservableProperty] private string _tuzatishQiymat = "";
    [ObservableProperty] private string _tuzatishSabab = "";
    [ObservableProperty] private string _tuzatishXato = "";

    /// <summary>Har aparatning yopishdagi (oxirgi) segmenti — tuzatiladigan qiymat shu.</summary>
    public List<SmenaKorsatkichDto> TuzatishAparatlari => (Tafsilot?.Korsatkichlar ?? []).GroupBy(k => k.AparatId).Select(g => g.Last()).OrderBy(k => k.AparatRaqam).ToList();
    public string TuzatishBoshi => TuzatishAparat is { } k ? Format.Son(k.Boshi) : "";
    public string TuzatishJoriy => TuzatishAparat is { } k ? Format.Son(k.Oxiri) : "";
    private decimal? TuzatishQ => Format.KasrOl(TuzatishQiymat);
    public bool TuzatishKichik => TuzatishAparat is { } k && TuzatishQ is { } q && q < k.Boshi;
    public string TuzatishNatija
    {
        get
        {
            if (TuzatishAparat is not { } k || TuzatishQ is not { } q || q < k.Boshi || D is not { } d) return "";
            var litr = SmenaHisobi.Litr(k.Boshi, q);
            var summa = SmenaHisobi.Summa(litr, k.Narx);
            var kutilgan = d.Kutilgan + (summa - k.Summa);
            var farq = (d.SanalganNaqd ?? 0) - kutilgan;
            return Til.F("Smenalar_TuzatishNatija", Format.Son(litr), Format.Pul(summa), Format.Pul(kutilgan), Format.Farq(farq));
        }
    }

    partial void OnTuzatishAparatChanged(SmenaKorsatkichDto? value) { TuzatishQiymat = value is null ? "" : Format.Son(value.Oxiri); TuzatishYangila(); }
    partial void OnTuzatishQiymatChanged(string value) => TuzatishYangila();
    private void TuzatishYangila()
    {
        foreach (var n in new[] { nameof(TuzatishBoshi), nameof(TuzatishJoriy), nameof(TuzatishKichik), nameof(TuzatishNatija) }) OnPropertyChanged(n);
    }

    [RelayCommand]
    private void TuzatishniBoshla()
    {
        if (!TuzataOladi) return;
        OnPropertyChanged(nameof(TuzatishAparatlari));
        TuzatishAparat = TuzatishAparatlari.FirstOrDefault();
        TuzatishSabab = TuzatishXato = "";
        TuzatishOchiq = true;
    }

    [RelayCommand] private void TuzatishniYop() => TuzatishOchiq = false;

    [RelayCommand]
    private async Task TuzatishniSaqla()
    {
        if (D is not { } d || TuzatishAparat is not { } k) return;
        if (TuzatishQ is not { } q) { TuzatishXato = Til.T("Smenalar_TuzatishXatoQiymat"); return; }
        if (q < k.Boshi) { TuzatishXato = Til.F("Smenalar_TuzatishXatoKichik", Format.Son(k.Boshi)); return; }
        if (string.IsNullOrWhiteSpace(TuzatishSabab)) { TuzatishXato = Til.T("Smenalar_TuzatishXatoSabab"); return; }
        try
        {
            Tafsilot = await Malumot.KorsatkichTuzat(d.Id, new KorsatkichTuzatishDto(k.AparatId, q, TuzatishSabab.Trim()));
            TuzatishOchiq = false;
        }
        catch (ApiXatosi e) { TuzatishXato = e.Message; }
    }

    // ================= Excel =================

    [RelayCommand]
    private async Task Excel()
    {
        if (D is not { } d || Tafsilot is not { } t) return;
        string[] ustunlar = [Til.T("Smenalar_Aparat"), Til.T("Smenalar_Oldingi"), Til.T("Smenalar_Yangi"), Til.T("Smenalar_Narx"), Til.T("Smenalar_Litr"), Til.T("Smenalar_Summa")];
        var qatorlar = t.Korsatkichlar.Select(k => (new object?[] { $"{k.AparatRaqam} · {k.YoqilgiNomi}", k.Boshi, k.Oxiri, k.Narx, k.Litr, k.Summa }, false)).ToList();
        qatorlar.Add((new object?[] { "", null, null, null, null, null }, false));
        foreach (var p in PulHisobi) qatorlar.Add((new object?[] { p.Nomi, null, null, null, null, p.Qiymat }, false));
        qatorlar.Add((new object?[] { Til.T("Smenalar_KassadaKerak"), null, null, null, null, d.Kutilgan }, true));
        qatorlar.Add((new object?[] { Til.T("Smenalar_SanalganNaqd"), null, null, null, null, d.SanalganNaqd }, true));
        qatorlar.Add((new object?[] { Til.T("Yopish_Farq"), null, null, null, null, d.Farq }, true));
        try
        {
            var fayl = ExcelEksport.Saqla("Smenalar", $"smena-{d.Id}.xlsx", TSarlavha, TIzoh, ustunlar, qatorlar,
                [Til.T("Jami"), null, null, null, t.Korsatkichlar.Sum(k => k.Litr), t.Korsatkichlar.Sum(k => k.Summa)]);
            await Malumot.EksportniYoz("Smena", $"#{d.Id} — {System.IO.Path.GetFileName(fayl)}");
            Bildirish.Malumot($"{Til.T("FaylSaqlandi")}: {fayl}");
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { Bildirish.Xato(e.Message); }
    }
}
