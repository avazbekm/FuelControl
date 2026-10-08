using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Hisobot jadvali qatori (ko'rinish uchun formatlangan).</summary>
public sealed record HisobotQatori(HisobotQatoriDto Q, string Guruh, string? Izoh, string Ikkinchi)
{
    public bool IzohBor => Izoh is not null;
    public string Litr => Format.Son(Q.Litr);
    public string Savdo => Format.Pul(Q.Savdo);
    public string Plastik => Format.Pul(Q.Plastik);
    public string Depozit => Format.Pul(Q.Depozit);
    public string Nasiya => Format.Pul(Q.Nasiya);
    public string Qaytgan => Format.Pul(Q.QaytganNasiya);
    public string Xarajat => Format.Pul(Q.Xarajat);
    public string Naqd => Format.Pul(Q.NaqdSavdo);
    public long FarqQ => Q.Ortiqcha - Q.Kamomat;
    public string Farq => Format.Farq(FarqQ);
    public bool FarqManfiy => FarqQ < 0;
    public bool FarqMusbat => FarqQ > 0;
    public bool FarqNol => FarqQ == 0;
}

/// <summary>Aparat va bak jadvali qatori.</summary>
public sealed record HisobotAparatQatori(HisobotAparatDto A, YoqilgiBelgi Yoqilgi)
{
    public string Nomi => Til.F("Aparat_Raqami", A.Raqam);
    public string Boshida => Format.Son(A.BakBoshida);
    public string Kirim => A.Kirim > 0 ? "+" + Format.ButunLitr(A.Kirim) : "0";
    public string Sotildi => Format.Son(A.Sotildi);
    public string Oxirida => Format.Son(A.BakOxirida);
    public bool OxiriManfiy => A.BakOxirida < 0;
    public string Savdo => Format.Pul(A.Savdo);
}

/// <summary>
/// Hisobotlar: davr × operator × guruh (smena / kun / oy / operator). Faqat yopilgan smenalar, hisob serverda (/hisobot).
/// Aparat/bak jadvali operator filtriga bog'liq emas (§7.12). Excel — joriy hisobot shaklida.
/// </summary>
public partial class HisobotViewModel : ObservableObject
{
    public List<Foydalanuvchi> OperatorFiltri { get; private set; } = new();
    public ObservableCollection<HisobotQatori> Qatorlar { get; } = new();
    public List<HisobotAparatQatori> Aparatlar { get; private set; } = new();

    [ObservableProperty] private Foydalanuvchi? _tanlanganOperator;
    [ObservableProperty] private DateTime? _dan = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _gacha = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GSmena), nameof(GKun), nameof(GOy), nameof(GOperator), nameof(GuruhSarlavha), nameof(IkkinchiSarlavha), nameof(JadvalSarlavha))]
    private HisobotGuruhi _guruh = HisobotGuruhi.Smena;

    [ObservableProperty] private string _tezDavr = "oy";

    public bool GSmena => Guruh == HisobotGuruhi.Smena;
    public bool GKun => Guruh == HisobotGuruhi.Kun;
    public bool GOy => Guruh == HisobotGuruhi.Oy;
    public bool GOperator => Guruh == HisobotGuruhi.Operator;
    public bool TezKecha => TezDavr == "kecha";
    public bool Tez7 => TezDavr == "hafta";
    public bool TezOy => TezDavr == "oy";
    public bool TezOtgan => TezDavr == "otganOy";

    public string GuruhSarlavha => Til.T(Guruh switch
    {
        HisobotGuruhi.Kun => "Hisobot_ColKun", HisobotGuruhi.Oy => "Hisobot_ColOy", HisobotGuruhi.Operator => "Hisobot_ColOperator", _ => "Hisobot_ColSmena",
    }).ToUpperInvariant();
    /// <summary>Ikkinchi ustun: smena guruhida operator, qolganlarida smenalar soni.</summary>
    public string IkkinchiSarlavha => Til.T(GSmena ? "Hisobot_ColOperator" : "Hisobot_ColSmenalar").ToUpperInvariant();
    public string JadvalSarlavha => Til.T(Guruh switch
    {
        HisobotGuruhi.Kun => "Hisobot_GuruhKun", HisobotGuruhi.Oy => "Hisobot_GuruhOy", HisobotGuruhi.Operator => "Hisobot_GuruhOperator", _ => "Hisobot_JadvalSarlavha",
    }) is var t && GSmena ? t : $"{Til.T("Hisobot_Guruhlash")}: {t}";

    private readonly KechiktirilganIsh _yuklash;
    private HisobotDto? _n;

    public HisobotViewModel()
    {
        _yuklash = new KechiktirilganIsh(Yukla, 300);
        OperatorFiltriniQur();
        Malumot.Ozgardi += () =>
        {
            if (!OperatorFiltri.Skip(1).Select(o => o.Id).SequenceEqual(Malumot.Operatorlar.Select(o => o.Id))) OperatorFiltriniQur();
            if (!Malumot.Kirilgan) { _n = null; Korsat(); }
            ExcelCommand.NotifyCanExecuteChanged();
            Rejala();
        };
        Til.Ozgardi += () => { OperatorFiltriniQur(); Korsat(); OnPropertyChanged(string.Empty); };
    }

    private void OperatorFiltriniQur()
    {
        var id = TanlanganOperator?.Id ?? 0;
        OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("Hisobot_Hammasi") }, .. Malumot.Operatorlar];
        OnPropertyChanged(nameof(OperatorFiltri));
        TanlanganOperator = OperatorFiltri.FirstOrDefault(o => o.Id == id) ?? OperatorFiltri[0];
    }

    private void Rejala()
    {
        if (Malumot.Kirilgan && Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Hisobotlar)) _yuklash.Rejala();
    }

    public Task HozirYukla() => _yuklash.Bajar();

    partial void OnTanlanganOperatorChanged(Foydalanuvchi? value) => Rejala();
    partial void OnDanChanged(DateTime? value) { TezniTekshir(); Rejala(); }
    partial void OnGachaChanged(DateTime? value) { TezniTekshir(); Rejala(); }
    partial void OnGuruhChanged(HisobotGuruhi value) => Rejala();
    partial void OnTezDavrChanged(string value)
    {
        foreach (var n in new[] { nameof(TezKecha), nameof(Tez7), nameof(TezOy), nameof(TezOtgan) }) OnPropertyChanged(n);
    }

    [RelayCommand] private void GuruhTanla(string g) => Guruh = Enum.Parse<HisobotGuruhi>(g);

    private static (DateTime Dan, DateTime Gacha)? TezOraliq(string t)
    {
        var b = DateTime.Today;
        var oy = new DateTime(b.Year, b.Month, 1);
        return t switch
        {
            "kecha" => (b.AddDays(-1), b.AddDays(-1)),
            "hafta" => (b.AddDays(-6), b),
            "oy" => (oy, b),
            "otganOy" => (oy.AddMonths(-1), oy.AddDays(-1)),
            _ => null,
        };
    }

    [RelayCommand]
    private void TezTanla(string t)
    {
        if (TezOraliq(t) is not { } o) return;
        Dan = o.Dan; Gacha = o.Gacha;
        TezDavr = t;
    }

    /// <summary>Sanalar qo'lda o'zgarsa — mos tez davr belgilanadi (yoki hech biri).</summary>
    private void TezniTekshir()
    {
        TezDavr = new[] { "kecha", "hafta", "oy", "otganOy" }.FirstOrDefault(t => TezOraliq(t) is { } o && o.Dan == Dan?.Date && o.Gacha == Gacha?.Date) ?? "";
    }

    private DateOnly DanK => DateOnly.FromDateTime(Dan ?? DateTime.Today);
    private DateOnly GachaK => DateOnly.FromDateTime(Gacha ?? DateTime.Today);

    private async Task Yukla(Func<bool> dolzarb)
    {
        var opId = TanlanganOperator is { Id: > 0 } o ? o.Id : (int?)null;
        var n = await Malumot.Api.Hisobot(DanK, GachaK, opId, Guruh);
        if (!dolzarb() || !Malumot.Kirilgan) return;
        _n = n;
        Korsat();
        ExcelCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Server guruh qiymati: smena "41", kun "yyyy-MM-dd", oy "yyyy-MM", operator — ism.</summary>
    private (string Asosiy, string? Izoh) GuruhNomi(HisobotQatoriDto q)
    {
        if (q.Jami) return (Til.T("Jami"), null);
        return Guruh switch
        {
            HisobotGuruhi.Smena => ("#" + q.Guruh, q.Sana is { } s ? Format.QisqaSana(s) : null),
            HisobotGuruhi.Kun when DateTime.TryParseExact(q.Guruh, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var k) => (Format.Sana(k), null),
            HisobotGuruhi.Oy when DateTime.TryParseExact(q.Guruh, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var oy)
                => ($"{Til.T("OyNomlari").Split(',')[oy.Month - 1]} {oy.Year}", null),
            _ => (q.Guruh, null),
        };
    }

    private HisobotQatori Qator(HisobotQatoriDto q)
    {
        var (a, i) = GuruhNomi(q);
        var ikkinchi = q.Jami ? "" : GSmena ? q.OperatorIsmi ?? "" : Til.F("Hisobot_SmenaSoni", q.SmenaSoni);
        return new HisobotQatori(q, a, i, ikkinchi);
    }

    private void Korsat()
    {
        Qatorlar.Clear();
        // Smena va kun qatorlari — eskisidan yangisiga (dizayndagidek); oy/operator — server tartibida.
        var royxat = _n?.Qatorlar ?? [];
        if (Guruh is HisobotGuruhi.Smena or HisobotGuruhi.Kun)
            royxat = royxat.OrderBy(q => q.Sana).ThenBy(q => int.TryParse(q.Guruh, out var id) ? id : 0).ToArray();
        foreach (var q in royxat) Qatorlar.Add(Qator(q));
        Aparatlar = (_n?.Aparatlar ?? []).OrderBy(a => a.Raqam).Select(a => new HisobotAparatQatori(a,
            YoqilgiBelgi.Yarat(a.YoqilgiNomi, Malumot.Yoqilgilar.FirstOrDefault(y => y.Nomi == a.YoqilgiNomi)?.Rang ?? "#2F6BFF"))).ToList();
        OnPropertyChanged(string.Empty);
    }

    // ---- KPI va jami
    private HisobotQatoriDto J => _n?.Jami ?? new HisobotQatoriDto("", null, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, true, 0);
    public HisobotQatori? JamiQator => _n is null ? null : Qator(_n.Jami);
    public bool JamiBor => _n is not null && Qatorlar.Count > 0;
    public bool Bosh => _n is not null && Qatorlar.Count == 0;
    public string DavrMatni => Til.F("Hisobot_Davr", Format.Sana(DanK), Format.Sana(GachaK), J.SmenaSoni);
    private long Asos => Math.Max(1, J.NaqdSavdo + J.Plastik + J.Depozit + J.Nasiya);
    private string Foiz(long s) => $"{Math.Round(100.0 * s / Asos)}%";
    public string JamiSavdo => Format.Pul(J.Savdo);
    public string JamiSavdoIzoh => Til.F("Hisobot_KpiSomSmena", J.SmenaSoni);
    public string JamiLitr => Format.Son(J.Litr);
    public string JamiNaqd => Format.Pul(J.NaqdSavdo);
    public string NaqdFoiz => Foiz(J.NaqdSavdo);
    public string JamiPlastik => Format.Pul(J.Plastik);
    public string PlastikFoiz => Foiz(J.Plastik);
    public string JamiDepozit => Format.Pul(J.Depozit);
    public string DepozitFoiz => Foiz(J.Depozit);
    public string JamiNasiya => Format.Pul(J.Nasiya);
    public string QaytganIzoh => Til.F("Hisobot_KpiQaytgan", Format.Pul(J.QaytganNasiya));
    public string JamiXarajat => Format.Pul(J.Xarajat);
    public string XarajatIzoh => Til.F("Hisobot_KpiXarajatYozuv", J.XarajatSoni);
    public string JamiKamomat => Format.Pul(J.Kamomat);
    public string OrtiqchaIzoh => Til.F("Hisobot_KpiOrtiqcha", Format.Pul(J.Ortiqcha));
    public string JamiAvans => Format.Pul(_n?.Avans ?? 0);

    // Aparat jadvali jami
    public string ABoshida => Format.Son(Aparatlar.Sum(a => a.A.BakBoshida));
    public string AKirim => "+" + Format.ButunLitr(Aparatlar.Sum(a => a.A.Kirim));
    public string ASotildi => Format.Son(Aparatlar.Sum(a => a.A.Sotildi));
    public string AOxirida => Format.Son(Aparatlar.Sum(a => a.A.BakOxirida));
    public string ASavdo => Format.Pul(Aparatlar.Sum(a => a.A.Savdo));
    public bool AparatBor => Aparatlar.Count > 0;
    public bool OperatorTanlangan => TanlanganOperator is { Id: > 0 };

    private bool EksportMumkin() => _n is not null && Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Eksport);
    public bool EksportKorinsin => Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Eksport);

    /// <summary>Joriy hisobot → Documents\FuelControl\Hisobotlar\*.xlsx: asosiy jadval, jami, keyin aparat/bak jadvali; eksport server auditiga yoziladi.</summary>
    [RelayCommand(CanExecute = nameof(EksportMumkin))]
    private async Task Excel()
    {
        if (_n is not { } n) return;
        string[] ustunlar = [GuruhSarlavha, IkkinchiSarlavha, Til.T("Hisobot_ColLitr"), Til.T("Hisobot_ColSavdo"), Til.T("Hisobot_ColPlastik"),
            Til.T("Hisobot_ColDepozit"), Til.T("Hisobot_ColNasiya"), Til.T("Hisobot_ColQaytgan"), Til.T("Hisobot_ColXarajat"), Til.T("Hisobot_ColNaqd"),
            Til.T("Kamomat"), Til.T("Hisobot_ColOrtiqcha")];
        object?[] Q(HisobotQatoriDto q)
        {
            var r = Qator(q);
            return [r.Izoh is { } i ? $"{r.Guruh} ({i})" : r.Guruh, r.Ikkinchi, q.Litr, q.Savdo, q.Plastik, q.Depozit, q.Nasiya, q.QaytganNasiya, q.Xarajat, q.NaqdSavdo, q.Kamomat, q.Ortiqcha];
        }
        var qatorlar = n.Qatorlar.Select(q => (Q(q), false)).ToList();
        qatorlar.Add((Q(n.Jami), true));
        qatorlar.Add((new object?[ustunlar.Length], false));
        qatorlar.Add((new object?[] { Til.T("Hisobot_BakSarlavha"), Til.T("Hisobot_ColYoqilgi"), Til.T("Hisobot_ColBakBoshida"), Til.T("Hisobot_ColKirim"),
            Til.T("Hisobot_ColSotildi"), Til.T("Hisobot_ColBakOxirida"), Til.T("Hisobot_ColSavdo") }, true));
        foreach (var a in n.Aparatlar.OrderBy(a => a.Raqam))
            qatorlar.Add((new object?[] { Til.F("Aparat_Raqami", a.Raqam), a.YoqilgiNomi, a.BakBoshida, a.Kirim, a.Sotildi, a.BakOxirida, a.Savdo }, false));

        var davr = $"{DanK:yyyy-MM-dd}_{GachaK:yyyy-MM-dd}";
        var opNomi = TanlanganOperator is { Id: > 0 } o ? o.ToliqIsm : Til.T("Hisobot_Hammasi");
        try
        {
            var fayl = ExcelEksport.Saqla("Hisobotlar", $"hisobot-{Guruh.ToString().ToLowerInvariant()}-{davr}.xlsx",
                Til.T("Hisobotlar"), $"{DavrMatni} · {opNomi} · {JadvalSarlavha}", ustunlar, qatorlar, null);
            await Malumot.EksportniYoz("Hisobot", $"{Format.Sana(DanK)} — {Format.Sana(GachaK)}, {opNomi}, {Guruh} — {System.IO.Path.GetFileName(fayl)}");
            Bildirish.Malumot($"{Til.T("FaylSaqlandi")}: {fayl}");
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            Bildirish.Xato(e.Message);
        }
    }
}
