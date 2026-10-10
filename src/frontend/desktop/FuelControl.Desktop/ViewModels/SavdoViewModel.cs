using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Yoqilg'i "pill" ranglari: nuqta = yoqilg'i rangi, fon = shu rang shaffof, matn = shu rang.</summary>
public sealed record YoqilgiBelgi(string Nomi, IBrush Nuqta, IBrush Fon, IBrush Matn)
{
    public static YoqilgiBelgi Yarat(string nomi, string rang)
    {
        var c = Color.TryParse(rang, out var r) ? r : Color.Parse("#2F6BFF");
        return new YoqilgiBelgi(nomi, new SolidColorBrush(c), new SolidColorBrush(Color.FromArgb(0x2E, c.R, c.G, c.B)), new SolidColorBrush(c));
    }

    public static YoqilgiBelgi Ol(YoqilgiTuri y) => Yarat(y.Nomi, y.Rang);
}

/// <summary>Savdo sahifasidagi aparat kartasi (oxirgi yopilgan smena holati).</summary>
public sealed record AparatKartasi(Aparat A, YoqilgiBelgi Yoqilgi)
{
    public string Nomi => Til.F("Aparat_Raqami", A.Raqam);
    public string Narx => Format.Pul(A.Yoqilgi.Narx) + " " + Til.T("Savdo_SomLitr");
    public string Pult => Format.Son(A.TotalLitr);
    public string Bak => Format.ButunLitr(A.BakQoldiq);
    public string BakAniq => Format.Son(A.BakQoldiq);
    public bool BakManfiy => A.BakManfiy;
    public string OxirgiKirim => A.OxirgiKirimVaqti is { } v
        ? Til.F("Bak_OxirgiKirim", Format.QisqaSana(v), Format.ButunLitr(A.OxirgiKirimLitr ?? 0))
        : Til.T("Bak_KirimYoq");
}

/// <summary>Ro'yxat qatori (nasiya, qaytish, xarajat) — o'chirish tugmasi bilan.</summary>
public sealed record YozuvQatori(int Id, string Tur, string Sarlavha, string Izoh, string Summa, string? Belgi, string BelgiKlassi,
    string? Harflar, string? Raqam, bool OchirishMumkin);

/// <summary>Plastik summasi qatori (§8.9): nomsiz, bo'sh qator hisobga olinmaydi; manfiy yoki noto'g'ri yozuv — xato.</summary>
public sealed partial class PlastikQatori(Action ozgardi) : ObservableObject
{
    [ObservableProperty] private string _summa = "";
    /// <summary>Qator tartib raqami (1 dan) — "N-plastik summasi" va "N-qatorni o'chirish" uchun.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaYorligi), nameof(OchirishYorligi))]
    private int _tartib;
    public string SummaYorligi => Til.F("Yopish_PlastikSummasi", Tartib);
    public string OchirishYorligi => Til.F("Yopish_QatorniOchir", Tartib);
    public bool Bosh => Summa.Trim().Length == 0;
    public long? Qiymat => Bosh || Summa.Contains('-') || Summa.Contains('−') ? null : Format.PulOl(Summa);
    public bool Xato => !Bosh && Qiymat is null;
    partial void OnSummaChanged(string value) { OnPropertyChanged(nameof(Xato)); ozgardi(); }
}

/// <summary>Nasiya kartasi qatori: muddati o'tgan (qizil) yoki muddati yaqin (neytral) qarz (§8.7).</summary>
public sealed record OtganQarz(NasiyaDto N)
{
    public bool Otgan => N.Holati == NasiyaHolati.MuddatiOtgan;
    public string Summa => Format.Pul(N.Qoldiq);
    public string MuddatEdi => Til.F(Otgan ? "Savdo_MuddatEdi" : "Savdo_MuddatGacha", Format.QisqaSana(N.Muddat));
    /// <summary>"N kun o'tdi" / "bugun" / "ertaga" / "N kun qoldi".</summary>
    public string KunOtdi => QaytishDialogVM.NasiyaKun(N.MuddatgachaKun);
    public string KunKlassi => Otgan ? "qizil" : N.MuddatgachaKun <= 1 ? "sariq" : "kok";
    public string Raqam => N.MashinaRaqami;
    public bool RaqamBor => !string.IsNullOrWhiteSpace(N.MashinaRaqami);
}

/// <summary>Yopish jadvalining aparat qatori: oldingi (§7.1) → yangi pult ko'rsatkichi → sotilgan litr va summa.</summary>
public partial class YopishQatori : ObservableObject
{
    public Aparat A { get; }
    public YoqilgiBelgi Yoqilgi { get; }
    public decimal Oldingi { get; }
    public long Narx => A.Yoqilgi.Narx;
    /// <summary>Narx o'zgarishida qayd etilgan (eski narxdagi) segmentlar — yopishda alohida qator bo'lib qo'shiladi.</summary>
    public IReadOnlyList<SmenaKorsatkichDto> Qayd { get; }
    private readonly Action _ozgardi;

    [ObservableProperty] private string _yangi = "";

    public YopishQatori(Aparat a, IReadOnlyList<SmenaKorsatkichDto> qayd, Action ozgardi)
    {
        A = a; Qayd = qayd; _ozgardi = ozgardi;
        Yoqilgi = YoqilgiBelgi.Ol(a.Yoqilgi);
        Oldingi = SmenaHisobi.Oldingi(a, qayd);
    }

    public string Nomi => Til.F("Aparat_Raqami", A.Raqam);
    public string OldingiMatn => Format.Son(Oldingi);
    public string NarxMatn => Format.Pul(Narx);

    public decimal? Qiymat => Format.KasrOl(Yangi);
    public bool Xato => (Qiymat is { } q && q < Oldingi) || (Yangi.Trim().Length > 0 && Qiymat is null);
    public string XatoMatn => Til.F("Yopish_OldingidanKichik", Format.Son(Oldingi));
    public decimal? Litr => Qiymat is { } q && q >= Oldingi ? SmenaHisobi.Litr(Oldingi, q) : null;
    public long? Summa => Litr is { } l ? SmenaHisobi.Summa(l, Narx) : null;
    public string LitrMatn => Litr is { } l ? Format.Son(l) + " L" : "—";
    public string SummaMatn => Summa is { } s ? Format.Pul(s) : "—";

    public bool QaydBor => Qayd.Count > 0;
    public long QaydSumma => Qayd.Sum(k => k.Summa);
    public decimal QaydLitr => Qayd.Sum(k => k.Litr);
    public string QaydMatn => string.Join("\n", Qayd.Select(k =>
        Til.F("Yopish_EskiNarxda", Nomi, Format.Son(k.Litr), Format.Pul(k.Narx), Format.Pul(k.Summa))));

    partial void OnYangiChanged(string value)
    {
        foreach (var n in new[] { nameof(Qiymat), nameof(Xato), nameof(Litr), nameof(Summa), nameof(LitrMatn), nameof(SummaMatn) })
            OnPropertyChanged(n);
        _ozgardi();
    }
}

/// <summary>
/// Savdo: ochiq smena holati (qoldiqlar, aparatlar, nasiya/xarajat), smena yopiq bo'lsa ochish formasi,
/// va yopish ko'rinishi (§1.5–1.6: pult ko'rsatkichlari, terminal, depozit, sanalgan naqd — jonli hisob, server bilan bir xil formula).
/// </summary>
public partial class SavdoViewModel : ObservableObject
{
    private static Foydalanuvchi Joriy => Malumot.JoriyFoydalanuvchi;
    private static SmenaTafsilotDto? T => Malumot.Joriy;
    private static Smena? S => Malumot.JoriySmena;

    public SavdoViewModel()
    {
        PlastikniTozala();
        Yukla();
        Malumot.Ozgardi += Yukla;
        Malumot.AloqaOzgardi += Buyruqlar;
        Til.Ozgardi += () => { Yukla(); OnPropertyChanged(string.Empty); };
        // Davomiylik matni yangilanib tursin (har 30 s). DispatcherTimer emas, Task.Delay — taymer ishga tushmay qolardi.
        _ = DavomiylikniYangilab();
    }

    private async Task DavomiylikniYangilab()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            Dispatcher.UIThread.Post(() => { if (SmenaOchiq) { OnPropertyChanged(nameof(SmenaIzoh)); OnPropertyChanged(nameof(Davomiylik)); } });
        }
    }

    // ================= Holat =================

    public bool SmenaOchiq => S is not null;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OchiqKorinsin), nameof(YopiqKorinsin))]
    private bool _yopishRejimi;
    public bool OchiqKorinsin => SmenaOchiq && !YopishRejimi;
    public bool YopiqKorinsin => !SmenaOchiq;

    public string SmenaIzoh => S is { } s
        ? $"#{s.Id} · {s.Operator.ToliqIsm} · {Format.QisqaSanaVaqt(s.Boshlandi)} {Til.T("Savdo_Dan")} · {s.Davomiylik}"
        : OxirgiYopilgan is { } o ? Til.F("Savdo_OxirgiSmenaYopdi", o.Id, o.Operator.ToliqIsm) : "";

    // ================= Ochiq smena =================

    public string Qaytim => Format.Pul(S?.OchishQaytim ?? 0);
    public string OchishTerminal => Format.Pul(S?.OchishTerminal ?? 0);
    public string OchishDepozit => Format.Pul(S?.OchishDepozit ?? 0);
    public string Bosqich1Izoh => Til.F("Savdo_Bosqich1Izoh", S is { } s ? Format.Vaqt(s.Boshlandi) : "");
    public string JoriySmenaSarlavha => Til.F("Savdo_JoriySmena", S?.Id ?? 0);
    public string OperatorIsmi => S?.Operator.ToliqIsm ?? "";
    public string Ochildi => S is { } s ? $"{Format.QisqaSana(s.Boshlandi)} · {Format.Vaqt(s.Boshlandi)}" : "";
    public string Davomiylik => S?.Davomiylik ?? "";
    public string NasiyaJami => Format.Pul(S?.NasiyaJami ?? 0);
    public string QaytganJami => Format.Pul(S?.QaytganNasiya ?? 0);
    public string XarajatJami => Format.Pul(S?.XarajatJami ?? 0);

    public List<AparatKartasi> Aparatlar { get; private set; } = new();
    public string AparatlarIzoh => Til.F("Savdo_AparatlarIzoh", OxirgiYopilgan?.Tugadi is { } t ? Format.QisqaSanaVaqt(t) : "—");

    // Joriy smena yozuvlari (nasiya / qaytish / xarajat) har doim AYNAN hozirgi smena tafsilotidan (T) olinadi:
    // T almashsa (yangi smena — o'zi ochgan yoki SignalR), ro'yxatlar o'qilish paytida qayta quriladi — oldingi smena
    // yozuvlari va "N ta" belgilari yangi smena sarlavhasi bilan birga bir lahza ham ko'rinmaydi.
    private object? _royxatManbasi = new();
    private List<YozuvQatori> _nasiyalar = new(), _qaytishlar = new(), _xarajatlar = new();
    public List<YozuvQatori> Nasiyalar { get { RoyxatlarniTekshir(); return _nasiyalar; } }
    public List<YozuvQatori> Qaytishlar { get { RoyxatlarniTekshir(); return _qaytishlar; } }
    public List<YozuvQatori> Xarajatlar { get { RoyxatlarniTekshir(); return _xarajatlar; } }
    private void RoyxatlarniTekshir() { if (!ReferenceEquals(_royxatManbasi, T)) RoyxatlarniQur(); }
    public bool NasiyaYoq => Nasiyalar.Count == 0 && Qaytishlar.Count == 0;
    public string NasiyaSoniBelgi => Til.F("Yopish_Ta", Nasiyalar.Count);
    public string QaytishSoniBelgi => Til.F("Yopish_Ta", Qaytishlar.Count);
    public string XarajatSoniBelgi => Til.F("Yopish_Ta", Xarajatlar.Count);
    public bool XarajatYoq => Xarajatlar.Count == 0;
    public string NasiyaIzoh => Til.F("Savdo_NasiyaShuSmenada", Nasiyalar.Count, Format.Pul(S?.NasiyaJami ?? 0));
    public string XarajatIzoh => Til.F("Savdo_XarajatIzoh", Xarajatlar.Count);

    public List<OtganQarz> OtganQarzlar { get; private set; } = new();
    /// <summary>
    /// Nasiya kartasi (§8.7, tuzatilgan): faol qarz bo'lsa ko'rinadi. Muddati o'tgan bo'lsa — qizil "Muddati o'tgan qarzlar"
    /// va o'tganlar ro'yxati; o'tgan yo'q bo'lsa — neytral "Nasiyalar" kartasi, eng yaqin 3 ta muddat ("N kun qoldi") bilan.
    /// Avval karta har doim qizil va "o'tgan" sarlavhali edi — yangi (muddati bor) nasiya ham "o'tgan" bo'lib ko'rinardi.
    /// </summary>
    public bool OtganKorinsin => Joriy.Bor(Ruxsat.Nasiyalar) && (Malumot.FaolNasiyalar?.Xulosa.FaolSoni ?? 0) > 0;
    public bool OtganBor => (Malumot.FaolNasiyalar?.Xulosa.MuddatiOtganSoni ?? 0) > 0;
    public string NasiyaKartaSarlavha => Til.T(OtganBor ? "Savdo_MuddatiOtganQarzlar" : "Savdo_Nasiyalar");
    public bool OtganRoyxatBor => OtganQarzlar.Count > 0;
    public string JamiQarzdorlik => Til.F("Savdo_JamiQarzdorlik", Format.Pul(Malumot.FaolNasiyalar?.Xulosa.FaolQarz ?? 0),
        Malumot.FaolNasiyalar?.Xulosa.FaolSoni ?? 0);
    public string OtganIzoh => OtganBor
        ? Til.F("Savdo_MijozSumma", Malumot.FaolNasiyalar?.Xulosa.MuddatiOtganSoni ?? 0, Format.Pul(Malumot.FaolNasiyalar?.Xulosa.MuddatiOtgan ?? 0))
        : Til.T("Savdo_YaqinMuddatlar");

    // Ruxsatlar
    public bool NasiyaYozaOladi => Joriy.Bor(Ruxsat.NasiyaYozish);
    public bool QarzQaytdiOladi => Joriy.Bor(Ruxsat.QarzQaytdi) && Joriy.Bor(Ruxsat.Nasiyalar);
    public bool XarajatYozaOladi => Joriy.Bor(Ruxsat.XarajatYozish);
    public bool BakKirimOladi => Joriy.Bor(Ruxsat.BakKirim) && !AparatYoq;
    /// <summary>Bo'sh baza (toza o'rnatish): aparat yo'q — smena ochilmaydi, yo'l-yo'riq ko'rsatiladi.</summary>
    public bool AparatYoq => Malumot.Aparatlar.Count == 0;
    public bool SozlamalarOchaOladi => Joriy.Bor(Ruxsat.Sozlamalar);
    [RelayCommand] private void SozlamalargaOt() => Navigatsiya.Sozlamalar(1, MainViewModel.SSozlamalar);
    /// <summary>Operator faqat o'z smenasini yopadi; "boshliq" (Smenalar ruxsati) boshqa operatornikini ham.</summary>
    public bool YopaOladi => S is { } s && Joriy.Bor(Ruxsat.SmenaYopish) && (s.Operator.Id == Joriy.Id || Joriy.Boshliqmi);
    public bool OchaOladi => Joriy.Bor(Ruxsat.SmenaOchish);
    public bool OchishRuxsatiYoq => !OchaOladi;

    // ================= Yopiq smena: ochish formasi =================

    [ObservableProperty] private string _ochQaytim = "";
    [ObservableProperty] private string _ochTerminal = "";
    [ObservableProperty] private string _ochDepozit = "";
    [ObservableProperty] private string _ochishXato = "";

    public string OchishFooter => Til.F("Savdo_OchishVaqti", Joriy.ToliqIsm, Format.Vaqt(DateTime.Now));
    public string JoriyHarflar => Joriy.BoshHarflar;

    public Smena? OxirgiYopilgan => Malumot.OxirgiYopilgan;
    public bool OxirgiBor => OxirgiYopilgan is not null;
    public string OxirgiSarlavha => Til.F("Savdo_OxirgiSmena", OxirgiYopilgan?.Id ?? 0);
    public string OxirgiIzoh => OxirgiYopilgan is { } o ? $"{o.Operator.ToliqIsm} · {Til.F("Savdo_Soat", (int)Math.Round(((o.Tugadi ?? o.Boshlandi) - o.Boshlandi).TotalHours))}" : "";
    public string OxirgiSavdo => Format.Pul(OxirgiYopilgan?.Savdo ?? 0);
    public string OxirgiLitr => Format.Son(OxirgiYopilgan?.JamiLitr ?? 0) + " L";
    public string OxirgiPlastik => Format.Pul(OxirgiYopilgan?.Plastik ?? 0);
    public string OxirgiDepozit => OxirgiYopilgan is { } o ? (o.DepozitFarqi > 0 ? "+" : "") + Format.Pul(o.DepozitFarqi) : "";
    public string OxirgiNasiya => Format.Pul(OxirgiYopilgan?.NasiyaJami ?? 0);
    public string OxirgiXarajat => Format.Pul(OxirgiYopilgan?.XarajatJami ?? 0);
    public bool OxirgiKamomat => OxirgiYopilgan?.Farq < 0;
    public bool OxirgiOrtiqcha => OxirgiYopilgan?.Farq > 0;
    public bool OxirgiTeng => OxirgiYopilgan?.Farq == 0;
    public string OxirgiFarq => OxirgiYopilgan is { } o ? Format.Farq(o.Farq) : "";
    public string OxirgiFarqIzoh => OxirgiYopilgan is { } o
        ? o.Farq < 0 ? Til.F("Savdo_OylikdanAyirildi", o.Operator.ToliqIsm) : Til.F("Savdo_HisobigaYozildi", o.Operator.ToliqIsm)
        : "";
    public string AparatlarHolatiIzoh => OxirgiYopilgan is { } o ? Til.F("Savdo_SmenaYopilgandanKeyin", o.Id) : Til.T("Savdo_HozirgiHolat");

    private bool OchishMumkin() => Malumot.AloqaBor && OchaOladi && !SmenaOchiq && !AparatYoq &&
        Format.PulOl(OchQaytim) is not null && Format.PulOl(OchTerminal) is not null && Format.PulOl(OchDepozit) is not null;

    partial void OnOchQaytimChanged(string value) => SmenaOchCommand.NotifyCanExecuteChanged();
    partial void OnOchTerminalChanged(string value) => SmenaOchCommand.NotifyCanExecuteChanged();
    partial void OnOchDepozitChanged(string value) => SmenaOchCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(OchishMumkin))]
    private async Task SmenaOch()
    {
        OchishXato = "";
        try
        {
            await Malumot.SmenaOch(new SmenaOchishDto(Format.PulOl(OchQaytim)!.Value, Format.PulOl(OchTerminal)!.Value, Format.PulOl(OchDepozit)!.Value));
            OchQaytim = OchTerminal = OchDepozit = "";
        }
        catch (ApiXatosi e) { OchishXato = e.Message; }
    }

    // ================= Yopish ko'rinishi =================

    public ObservableCollection<YopishQatori> YopishQatorlari { get; } = new();
    /// <summary>Plastik summalari (§8.9): terminal, kassa aparati, nollash cheki… — har biri alohida qator; boshida bitta bo'sh qator.</summary>
    public ObservableCollection<PlastikQatori> PlastikQatorlari { get; } = new();
    public const int PlastikMaks = 20;   // server cheklovi
    /// <summary>Yangi qator qo'shildi — ko'rinish unga fokus beradi.</summary>
    public event Action<PlastikQatori>? PlastikQoshildi;
    [ObservableProperty] private string _yopDepozit = "";
    [ObservableProperty] private string _sanalganNaqd = "";
    [ObservableProperty] private string _izoh = "";
    [ObservableProperty] private string _yopishXato = "";

    public string YopishBelgi => Til.F("Yopish_SmenaN", S?.Id ?? 0);
    public string YopishIzohi => S is { } s ? $"{s.Operator.ToliqIsm} · {Format.QisqaSanaVaqt(s.Boshlandi)} {Til.T("Savdo_Dan")} · {s.Davomiylik}" : "";

    /// <summary>Bo'sh bo'lmagan plastik summalari; birortasi noto'g'ri bo'lsa yoki hammasi bo'sh bo'lsa — null.</summary>
    private List<long>? PlastikSummalari =>
        PlastikQatorlari.Any(q => q.Xato) || PlastikQatorlari.All(q => q.Bosh) ? null
        : PlastikQatorlari.Where(q => !q.Bosh).Select(q => q.Qiymat!.Value).ToList();
    /// <summary>Yopishdagi terminal (jami plastik) = qatorlar yig'indisi.</summary>
    private long? TerminalQ => PlastikSummalari?.Sum();
    private long? DepozitQ => Format.PulOl(YopDepozit);
    private long? NaqdQ => Format.PulOl(SanalganNaqd);
    public bool TerminalXato => PlastikQatorlari.Any(q => q.Xato);
    public string JamiPlastikMatn => TerminalQ is { } t ? Format.Pul(t) : "—";
    /// <summary>"Jami plastik" qatori — ikki va undan ko'p summa yozilganda.</summary>
    public bool PlastikKopQator => PlastikQatorlari.Count(q => !q.Bosh) >= 2;
    public bool PlastikQoshMumkin => PlastikQatorlari.Count < PlastikMaks;

    private void PlastikniTozala()
    {
        PlastikQatorlari.Clear();
        PlastikQatorlari.Add(new PlastikQatori(YopishHisobla) { Tartib = 1 });
    }

    private void PlastikRaqamla()
    {
        for (int i = 0; i < PlastikQatorlari.Count; i++) PlastikQatorlari[i].Tartib = i + 1;
    }

    [RelayCommand]
    private void PlastikQosh()
    {
        if (!PlastikQoshMumkin) return;
        var q = new PlastikQatori(YopishHisobla);
        PlastikQatorlari.Add(q);
        PlastikRaqamla();
        YopishHisobla();
        PlastikQoshildi?.Invoke(q);
    }

    [RelayCommand]
    private void PlastikOchir(PlastikQatori q)
    {
        PlastikQatorlari.Remove(q);
        if (PlastikQatorlari.Count == 0) PlastikQatorlari.Add(new PlastikQatori(YopishHisobla));
        PlastikRaqamla();
        YopishHisobla();
    }
    public bool DepozitXato => YopDepozit.Trim().Length > 0 && DepozitQ is null;

    public bool HammaKorsatkich => YopishQatorlari.Count > 0 && YopishQatorlari.All(q => q.Litr is not null);
    public decimal JamiLitr => YopishQatorlari.Sum(q => (q.Litr ?? 0) + q.QaydLitr);
    public long JamiSumma => YopishQatorlari.Sum(q => (q.Summa ?? 0) + q.QaydSumma);
    public string JamiLitrMatn => Format.Son(JamiLitr) + " L";
    public string JamiSummaMatn => Format.Pul(JamiSumma);
    public bool QaydBor => YopishQatorlari.Any(q => q.QaydBor);
    public string QaydMatn => string.Join("\n", YopishQatorlari.Where(q => q.QaydBor).Select(q => q.QaydMatn));

    public long? ShuSmenaPlastik => TerminalQ is { } t ? t - (S?.OchishTerminal ?? 0) : null;
    public long? DepozitFarqi => DepozitQ is { } d ? d - (S?.OchishDepozit ?? 0) : null;
    public string ShuSmenaPlastikMatn => ShuSmenaPlastik is { } p ? Format.Pul(p) : "—";
    public bool PlastikKam => ShuSmenaPlastik < 0;
    public string DepozitFarqiMatn => DepozitFarqi is { } d ? (d > 0 ? "+" : "") + Format.Pul(d) : "—";
    public bool DepozitManfiy => DepozitFarqi < 0;

    private SmenaNatijasi? Natija => S is { } s && HammaKorsatkich && TerminalQ is { } t && DepozitQ is { } d
        ? SmenaHisobi.Hisobla(s.OchishQaytim, s.OchishTerminal, s.OchishDepozit, JamiSumma, s.QaytganNasiya, s.NasiyaJami, s.XarajatJami, t, d, NaqdQ ?? 0)
        : null;

    // O'ng panel (kassadagi naqd)
    public string BQaytim => Qosh(S?.OchishQaytim ?? 0);
    public string BSavdo => HammaKorsatkich ? Qosh(JamiSumma) : "—";
    public string BQaytgan => Qosh(S?.QaytganNasiya ?? 0);
    public string BPlastik => Ayir(ShuSmenaPlastik);
    public string BDepozit => Ayir(DepozitFarqi);
    public string BNasiya => Ayir(S?.NasiyaJami);
    public string BXarajat => Ayir(S?.XarajatJami);
    public string Kutilgan => Natija is { } n ? Format.Pul(n.Kutilgan) : "—";
    public long? Farq => Natija is { } n && NaqdQ is not null ? n.Farq : null;
    public bool HolatToliqEmas => Natija is null;

    /// <summary>Hisob chiqmasa — nima yetishmaydi: har noto'g'ri/bo'sh aparat qatori, terminal va depozit alohida.</summary>
    public List<string> ToliqEmasSabablar
    {
        get
        {
            var l = new List<string>();
            foreach (var q in YopishQatorlari)
            {
                if (q.Qiymat is { } v && v < q.Oldingi) l.Add(Til.F("Yopish_SababKichik", q.Nomi, q.OldingiMatn));
                else if (q.Yangi.Trim().Length > 0 && q.Qiymat is null) l.Add(Til.F("Yopish_SababNotogri", q.Nomi));
                else if (q.Litr is null) l.Add(Til.F("Yopish_SababBosh", q.Nomi));
            }
            if (TerminalXato) l.Add(Til.T("Yopish_SababPlastikXato"));
            else if (TerminalQ is null) l.Add(Til.T("Yopish_SababTerminal"));
            if (DepozitQ is null) l.Add(Til.T("Yopish_SababDepozit"));
            return l;
        }
    }

    /// <summary>
    /// Faqat ba'zi aparat qatorlari yetishmasa (terminal va depozit kiritilgan): to'g'ri kiritilgan qatorlar bo'yicha
    /// taxminiy "kassada bo'lishi kerak" — aniq qaysi aparatlar hisobga olinmaganini aytadi. Yopish baribir to'liq hisob talab qiladi.
    /// </summary>
    public string TaxminiyMatn
    {
        get
        {
            if (Natija is not null || S is not { } s || TerminalQ is not { } t || DepozitQ is not { } d || !YopishQatorlari.Any(q => q.Litr is not null)) return "";
            var k = SmenaHisobi.Hisobla(s.OchishQaytim, s.OchishTerminal, s.OchishDepozit, JamiSumma, s.QaytganNasiya, s.NasiyaJami, s.XarajatJami, t, d, 0).Kutilgan;
            return Til.F("Yopish_Taxminiy", Format.Pul(k), string.Join(", ", YopishQatorlari.Where(q => q.Litr is null).Select(q => q.Nomi)));
        }
    }
    public bool TaxminiyBor => TaxminiyMatn.Length > 0;
    public bool HolatNaqd => Natija is not null && NaqdQ is null;
    public bool HolatKamomat => Farq < 0;
    public bool HolatOrtiqcha => Farq > 0;
    public bool HolatTeng => Farq == 0;
    public string FarqMatn => Farq is { } f ? Format.Farq(f) : "";
    public string KamomatIzoh => Til.F("Yopish_OylikdanAyiriladi", S?.Operator.ToliqIsm ?? "");

    /// <summary>Ayiriladigan qator: musbat → "−X", manfiy → "+X", nol → "0" (ishorasiz).</summary>
    private static string Ayir(long? n) => n switch { null => "—", 0 => "0", > 0 => "−" + Format.Pul(n.Value), _ => "+" + Format.Pul(-n.Value) };
    private static string Qosh(long n) => n == 0 ? "0" : "+" + Format.Pul(n);

    partial void OnYopDepozitChanged(string value) => YopishHisobla();
    partial void OnSanalganNaqdChanged(string value) => YopishHisobla();

    private void YopishHisobla()
    {
        foreach (var n in new[] { nameof(TerminalXato), nameof(DepozitXato), nameof(HammaKorsatkich), nameof(JamiLitrMatn), nameof(JamiSummaMatn),
                     nameof(ShuSmenaPlastikMatn), nameof(PlastikKam), nameof(DepozitFarqiMatn), nameof(DepozitManfiy),
                     nameof(BQaytim), nameof(BSavdo), nameof(BQaytgan), nameof(BPlastik), nameof(BDepozit), nameof(BNasiya), nameof(BXarajat),
                     nameof(Kutilgan), nameof(Farq), nameof(HolatToliqEmas), nameof(HolatNaqd), nameof(HolatKamomat), nameof(HolatOrtiqcha),
                     nameof(HolatTeng), nameof(FarqMatn), nameof(KamomatIzoh), nameof(QaydBor), nameof(QaydMatn),
                     nameof(ToliqEmasSabablar), nameof(TaxminiyMatn), nameof(TaxminiyBor),
                     nameof(JamiPlastikMatn), nameof(PlastikKopQator), nameof(PlastikQoshMumkin) })
            OnPropertyChanged(n);
        SmenaniYopCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Yopish formasi qaysi smena uchun to'ldirilgan. Smena Id o'zgarsa (yangi smena ochildi, boshqa operator ochdi,
    /// SignalR orqali keldi) forma to'liq tozalanadi; bitta smena ichida yopish sahifasidan chiqib-qaytganda qiymatlar saqlanadi.</summary>
    private int? _yopishSmenasi;

    private void YopishFormasiniSmenagaMosla()
    {
        if (S?.Id == _yopishSmenasi) return;
        _yopishSmenasi = S?.Id;
        YopishQatorlari.Clear();   // eski "Yangi, L" qiymatlari yangi smenaga o'tmasin
        PlastikniTozala();         // plastik — bitta bo'sh qatorga qaytadi
        YopDepozit = SanalganNaqd = Izoh = YopishXato = "";
    }

    [RelayCommand]
    private void YopishniBoshla()
    {
        if (!YopaOladi) return;
        YopishFormasiniSmenagaMosla();
        YopishQatorlariniQur();
        YopishXato = "";
        YopishRejimi = true;
        YopishHisobla();
    }

    [RelayCommand] private void SavdogaQayt() => YopishRejimi = false;

    private void YopishQatorlariniQur()
    {
        var eski = YopishQatorlari.ToDictionary(q => q.A.Id, q => q.Yangi);
        YopishQatorlari.Clear();
        var qayd = T?.Korsatkichlar ?? [];
        foreach (var a in Malumot.Aparatlar.OrderBy(a => a.Raqam))
        {
            var q = new YopishQatori(a, qayd.Where(k => k.AparatId == a.Id && k.NarxOzgarishida).ToList(), YopishHisobla);
            if (eski.TryGetValue(a.Id, out var y)) q.Yangi = y;
            YopishQatorlari.Add(q);
        }
    }

    private static string QatorImzosi()
    {
        var qayd = T?.Korsatkichlar ?? [];
        return string.Join("|", Malumot.Aparatlar.OrderBy(a => a.Raqam).Select(a =>
            $"{a.Id}:{SmenaHisobi.Oldingi(a, qayd.Where(k => k.AparatId == a.Id && k.NarxOzgarishida))}:{a.Yoqilgi.Narx}"));
    }

    private bool YopishMumkin() => Malumot.AloqaBor && Natija is not null && NaqdQ is not null && YopishQatorlari.All(q => !q.Xato);

    [RelayCommand(CanExecute = nameof(YopishMumkin))]
    private async Task SmenaniYop()
    {
        if (S is not { } s || Natija is not { } n) return;
        var farqMatn = n.Farq switch
        {
            < 0 => $"{Til.T("Savdo_Kamomat")}: {Format.Pul(-n.Farq)}",
            > 0 => $"{Til.T("Savdo_Ortiqcha")}: {Format.Pul(n.Farq)}",
            _ => Til.T("Savdo_FarqYoq"),
        };
        if (!await Bildirish.Tasdiqla(Til.T("Yopish_TasdiqSarlavha"),
                $"{Til.T("Yopish_KassadaBolishiKerak")}: {Format.Pul(n.Kutilgan)}\n{Til.T("Yopish_SanalganNaqd")}: {Format.Pul(NaqdQ!.Value)}\n{farqMatn}",
                Til.T("Yopish_Tasdiqlash")))
            return;
        YopishXato = "";
        try
        {
            var izoh = Izoh.Trim();
            await Malumot.SmenaYop(s.Id, new SmenaYopishDto(
                YopishQatorlari.Select(q => new AparatKorsatkichDto(q.A.Id, q.Qiymat!.Value)).ToArray(),
                TerminalQ!.Value, DepozitQ!.Value, NaqdQ!.Value, izoh.Length > 0 ? izoh : null, PlastikSummalari!.ToArray()));
            YopishRejimi = false;
        }
        catch (ApiXatosi e) { YopishXato = e.Message; }
    }

    // ================= Dialoglar va o'chirish =================

    [RelayCommand] private void NasiyaYoz() => Dialoglar.Nasiya.Och();
    [RelayCommand] private void QarzQaytdi() => Dialoglar.Qaytish.Och(null);
    [RelayCommand] private void XarajatYoz() => Dialoglar.Xarajat.Och();
    [RelayCommand] private void BakKirim() => Dialoglar.Bak.Och(null);
    [RelayCommand] private void BarchaNasiyalar() => Navigatsiya.Och(MainViewModel.SNasiyalar);

    [RelayCommand]
    private async Task Ochir(YozuvQatori q)
    {
        if (!await Bildirish.Tasdiqla(Til.T("Ochirish"), Til.F("Savdo_OchirishTasdiq", $"{q.Sarlavha} · {q.Summa}"), Til.T("Ochirish"), xavfli: true))
            return;
        try
        {
            switch (q.Tur)
            {
                case "nasiya": await Malumot.NasiyaOchir(q.Id); break;
                case "qaytish": await Malumot.QaytishOchir(q.Id); break;
                default: await Malumot.XarajatOchir(q.Id); break;
            }
        }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    // ================= Yuklash =================

    /// <summary>§7.4: o'z yozuvi (MuallifId) yoki boshliq.</summary>
    private bool OchiraOladi(int muallifId) => Joriy.Boshliqmi || muallifId == Joriy.Id;

    private void Yukla()
    {
        if (!SmenaOchiq && YopishRejimi) YopishRejimi = false;

        Aparatlar = Malumot.Aparatlar.OrderBy(a => a.Raqam).Select(a => new AparatKartasi(a, YoqilgiBelgi.Ol(a.Yoqilgi))).ToList();

        RoyxatlarniQur();
        var faol = Malumot.FaolNasiyalar?.Royxat ?? [];
        OtganQarzlar = (OtganBor ? faol.Where(n => n.Holati == NasiyaHolati.MuddatiOtgan) : faol.Where(n => n.Holati == NasiyaHolati.Faol))
            .OrderBy(n => n.MuddatgachaKun).Take(3).Select(n => new OtganQarz(n)).ToList();
        YuklaDavomi();
    }

    private void RoyxatlarniQur()
    {
        var t = T;
        _royxatManbasi = t;
        _nasiyalar = (t?.Nasiyalar ?? []).OrderBy(n => n.Yozildi).Select(n => new YozuvQatori(n.Id, "nasiya", n.MijozIsmi, Format.Telefon(n.Telefon),
            Format.Pul(n.Summa), Til.F("Savdo_MuddatGacha", Format.QisqaSana(n.Muddat)),
            n.MuddatgachaKun <= 3 ? "sariq" : "kok", Format.BoshHarflar(n.MijozIsmi), n.MashinaRaqami, OchiraOladi(n.MuallifId))).ToList();
        _qaytishlar = (t?.Qaytishlar ?? []).Select(q => new YozuvQatori(q.Id, "qaytish", q.MijozIsmi,
            $"{Til.T("Savdo_QarzniQaytardi")} · {Format.Vaqt(q.Vaqt.ToLocalTime())} · {Til.T("Tolov_" + q.Usul).ToLowerInvariant()}",
            "+" + Format.Pul(q.Summa), null, "", null, null, OchiraOladi(q.MuallifId))).ToList();
        _xarajatlar = (t?.Xarajatlar ?? []).OrderByDescending(x => x.Vaqt).Select(x => new YozuvQatori(x.Id, "xarajat", x.Sabab,
            $"{Format.Vaqt(x.Vaqt.ToLocalTime())} · {Til.T(x.Manba == XarajatManbai.Kassa ? "Savdo_Kassadan" : "Savdo_DepozitKartadan")}",
            Format.Pul(x.Summa), null, "", null, null, OchiraOladi(x.MuallifId))).ToList();
    }

    private void YuklaDavomi()
    {
        // Smena almashgan bo'lsa (yopildi / yangisi ochildi) — yopish formasi tozalanadi; ochiq turgan yopish sahifasi yangi smena bilan quriladi.
        if (_yopishSmenasi is not null && S?.Id != _yopishSmenasi)
        {
            YopishFormasiniSmenagaMosla();
            if (YopishRejimi) YopishQatorlariniQur();
        }
        // Yozib turilgan qatorlar faqat aparat yoki "oldingi" qiymat o'zgarsa qayta quriladi (fokus yo'qolmasin).
        if (YopishRejimi && QatorImzosi() != string.Join("|", YopishQatorlari.Select(q => $"{q.A.Id}:{q.Oldingi}:{q.Narx}")))
            YopishQatorlariniQur();
        OnPropertyChanged(string.Empty);
        Buyruqlar();
    }

    private void Buyruqlar()
    {
        SmenaOchCommand.NotifyCanExecuteChanged();
        SmenaniYopCommand.NotifyCanExecuteChanged();
    }
}
