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

/// <summary>Smena hisobi dialoglari — MainWindow'da parda ustida, istalgan sahifadan ochiladi.</summary>
public static class Dialoglar
{
    public static NasiyaDialogVM Nasiya { get; } = new();
    public static QaytishDialogVM Qaytish { get; } = new();
    public static XarajatDialogVM Xarajat { get; } = new();
    public static BakKirimDialogVM Bak { get; } = new();
    public static NarxDialogVM Narx { get; } = new();

    /// <summary>"Smena #42 · Alisher Karimov" — dialog sarlavhasi ostidagi matn.</summary>
    public static string SmenaMatni => Malumot.JoriySmena is { } s ? $"{Til.F("Yopish_SmenaN", s.Id)} · {s.Operator.ToliqIsm}" : "";
}

public abstract partial class DialogVM : ObservableObject
{
    [ObservableProperty] private bool _ochiq;
    [ObservableProperty] private string _xato = "";

    protected DialogVM() => Til.Ozgardi += () => OnPropertyChanged(string.Empty);

    [RelayCommand] private void Yop() => Ochiq = false;

    /// <summary>Saqlash: xato bo'lsa dialogda ko'rsatiladi, muvaffaqiyatda yopiladi.</summary>
    protected async Task Bajar(Func<Task> ish)
    {
        Xato = "";
        try { await ish(); Ochiq = false; }
        catch (ApiXatosi e) { Xato = e.Message; }
    }
}

// ========================= Nasiya yozish =========================

/// <summary>Mavjud mijoz taklifi (§8.2). Tanlangan — ↑/↓ bilan belgilangan qator (§8.8).</summary>
public sealed partial class MijozTaklifi(MijozTaklifDto m) : ObservableObject
{
    public MijozTaklifDto M { get; } = m;
    [ObservableProperty] private bool _tanlangan;
    public string Harflar => Format.BoshHarflar(M.MijozIsmi);
    public string Izoh => string.Join(" · ", new[] { Format.Telefon(M.Telefon), M.MashinaRaqami }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string Qarz => M.FaolQarz > 0 ? Til.F("Nasiya_TaklifQarz", Format.Pul(M.FaolQarz)) : Til.T("Nasiya_TaklifQarzYoq");
    public string QarzKlassi => M.FaolQarz > 0 ? "sariq" : "kulrang";
}

public partial class NasiyaDialogVM : DialogVM
{
    [ObservableProperty] private string _mijozIsmi = "";
    [ObservableProperty] private string _telefon = "";
    [ObservableProperty] private string _mashinaRaqami = "";

    /// <summary>Mavjud mijozlar takliflari (GET /nasiyalar/mijozlar?q=), 250 ms kechikish bilan.</summary>
    public ObservableCollection<MijozTaklifi> Takliflar { get; } = new();
    public bool TakliflarKorinsin => Takliflar.Count > 0;
    /// <summary>Takliflar ro'yxati qaysi maydon ostida ochiladi: "ism" (mijoz ismi) yoki "aloqa" (telefon / mashina raqami).
    /// Ro'yxat ustma-ust (overlay) chiziladi — paydo bo'lganda pastdagi maydonlar va "Saqlash" tugmasi siljimaydi.</summary>
    private string _taklifManba = "ism";
    public bool IsmTakliflari => TakliflarKorinsin && _taklifManba == "ism";
    public bool AloqaTakliflari => TakliflarKorinsin && _taklifManba == "aloqa";
    private int _tanlanganIndeks = -1;

    private void TakliflarYangilandi()
    {
        _tanlanganIndeks = -1;
        foreach (var n in new[] { nameof(TakliflarKorinsin), nameof(IsmTakliflari), nameof(AloqaTakliflari) }) OnPropertyChanged(n);
    }

    /// <summary>↑/↓: takliflar ichida yurish (ro'yxat ochiq bo'lsa). Qaytaradi — ishlatildimi.</summary>
    public bool TaklifniSur(int qadam)
    {
        if (!TakliflarKorinsin) return false;
        var i = _tanlanganIndeks < 0 ? (qadam > 0 ? 0 : Takliflar.Count - 1) : Math.Clamp(_tanlanganIndeks + qadam, 0, Takliflar.Count - 1);
        for (int k = 0; k < Takliflar.Count; k++) Takliflar[k].Tanlangan = k == i;
        _tanlanganIndeks = i;
        return true;
    }

    /// <summary>Enter: belgilangan taklif bo'lsa — tanlanadi (ism, telefon, mashina to'ladi). Qaytaradi — tanlandimi.</summary>
    public bool BelgilanganniTanla()
    {
        if (!TakliflarKorinsin || _tanlanganIndeks < 0 || _tanlanganIndeks >= Takliflar.Count) return false;
        Toldir(Takliflar[_tanlanganIndeks].M, faqatBosh: false);
        return true;
    }

    /// <summary>Esc yoki fokus boshqa maydonga o'tganda: ro'yxat yopiladi, matn o'zgarmaydi; kutilayotgan qidiruv bekor.</summary>
    public void TakliflarniYop()
    {
        _takliflarYuklash.BekorQil();
        _soralgan = "";
        if (Takliflar.Count == 0) return;
        Takliflar.Clear();
        TakliflarYangilandi();
    }

    /// <summary>Saqlashdagi xato qaysi maydonga tegishli — ko'rinish fokusni o'sha yerga o'tkazadi (§8.8).</summary>
    [ObservableProperty] private string _xatoMaydon = "";
    /// <summary>"Mavjud mijoz · faol qarz X" — tanlangan yoki telefon bo'yicha topilgan mijoz.</summary>
    [ObservableProperty] private string _mavjudIzoh = "";
    public bool MavjudBor => MavjudIzoh.Length > 0;
    partial void OnMavjudIzohChanged(string value) => OnPropertyChanged(nameof(MavjudBor));

    private bool _toldirilmoqda;
    private string _soralgan = "";
    private readonly Kechiktirgich _takliflarYuklash;

    public NasiyaDialogVM() => _takliflarYuklash = new Kechiktirgich(TakliflarniYukla, 250);

    partial void OnMijozIsmiChanged(string value) => MaydonOzgardi(value, "ism");
    partial void OnMashinaRaqamiChanged(string value) => MaydonOzgardi(value, "aloqa");
    partial void OnTelefonChanged(string value) => MaydonOzgardi(Format.TelefonRaqamlari(value), "aloqa");

    /// <summary>Foydalanuvchi yozganda — oxirgi tahrirlangan maydon bo'yicha qidiriladi (telefon — raqamlari bilan).</summary>
    private void MaydonOzgardi(string q, string manba)
    {
        if (_toldirilmoqda || !Ochiq) return;
        MavjudIzoh = "";
        _taklifManba = manba;
        _soralgan = q.Trim();
        if (_soralgan.Length == 0) { _takliflarYuklash.BekorQil(); Takliflar.Clear(); TakliflarYangilandi(); return; }
        _takliflarYuklash.Rejala();
    }

    private async Task TakliflarniYukla(Func<bool> dolzarb)
    {
        var q = _soralgan;
        if (q.Length == 0) return;
        var l = await Malumot.Api.MijozTakliflari(q) ?? new();
        if (!dolzarb() || !Ochiq || q != _soralgan) return;

        // Telefon to'liq kiritilib, mavjud mijozga to'g'ri kelsa — bo'sh maydonlar o'zi to'ladi (§8.2).
        var tel = Format.TelefonRaqamlari(Telefon);
        if (tel.Length == 9 && l.FirstOrDefault(m => Format.TelefonRaqamlari(m.Telefon) == tel) is { } mos)
        {
            Toldir(mos, faqatBosh: true);
            return;
        }
        Takliflar.Clear();
        foreach (var m in l) Takliflar.Add(new MijozTaklifi(m));
        TakliflarYangilandi();
    }

    private void Toldir(MijozTaklifDto m, bool faqatBosh)
    {
        _toldirilmoqda = true;
        try
        {
            if (!faqatBosh || string.IsNullOrWhiteSpace(MijozIsmi)) MijozIsmi = m.MijozIsmi;
            if (!faqatBosh || Format.TelefonRaqamlari(Telefon).Length == 0) Telefon = Format.Telefon(m.Telefon);
            if (!faqatBosh || string.IsNullOrWhiteSpace(MashinaRaqami)) MashinaRaqami = m.MashinaRaqami;
        }
        finally { _toldirilmoqda = false; }
        MavjudIzoh = Til.F("Nasiya_MavjudMijoz", Format.Pul(m.FaolQarz));
        _takliflarYuklash.BekorQil();
        Takliflar.Clear();
        TakliflarYangilandi();
    }

    [RelayCommand] private void TaklifniTanla(MijozTaklifi t) => Toldir(t.M, faqatBosh: false);
    [ObservableProperty] private string _summa = "";
    [ObservableProperty] private string _izoh = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MuddatBelgi), nameof(Kun3), nameof(Hafta1), nameof(Hafta2), nameof(Oy1))]
    private DateTime? _muddat;

    public string Kontekst => Dialoglar.SmenaMatni;
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.Today);
    private int? Kunlar => Muddat is { } m ? DateOnly.FromDateTime(m.Date).DayNumber - Bugun.DayNumber : null;
    public bool Kun3 => Kunlar == 3;
    public bool Hafta1 => Kunlar == 7;
    public bool Hafta2 => Kunlar == 14;
    public bool Oy1 => Muddat is { } m && DateOnly.FromDateTime(m.Date) == Bugun.AddMonths(1);
    public string MuddatBelgi => Muddat is { } m ? Til.F("Nasiya_MuddatIzoh", m.Date.ToString("dd.MM"), Kunlar ?? 0) : "";

    public void Och()
    {
        _toldirilmoqda = true;
        MijozIsmi = Telefon = MashinaRaqami = Summa = Izoh = Xato = "";
        _toldirilmoqda = false;
        MavjudIzoh = ""; _soralgan = ""; XatoMaydon = "";
        _takliflarYuklash.BekorQil();
        Takliflar.Clear(); TakliflarYangilandi();
        Muddat = DateTime.Today.AddDays(7);
        OnPropertyChanged(nameof(Kontekst));
        Ochiq = true;
    }

    [RelayCommand]
    private void MuddatTanla(string t) => Muddat = t switch
    {
        "3" => DateTime.Today.AddDays(3),
        "7" => DateTime.Today.AddDays(7),
        "14" => DateTime.Today.AddDays(14),
        _ => DateTime.Today.AddMonths(1),
    };

    private Task XatoBer(string kalit, string maydon)
    {
        XatoMaydon = "";
        Xato = Til.T(kalit);
        XatoMaydon = maydon;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task Saqla()
    {
        TakliflarniYop();
        // §7.10: ism, summa > 0, muddat majburiy; telefon yoki mashina raqamidan kamida bittasi; muddat bugundan oldin emas.
        if (string.IsNullOrWhiteSpace(MijozIsmi)) return XatoBer("Nasiya_XatoIsm", "ism");
        var telefon = Format.TelefonSaqlash(Telefon);
        if (telefon is null) return XatoBer("Nasiya_XatoTelefon", "telefon");
        if (telefon.Length == 0 && string.IsNullOrWhiteSpace(MashinaRaqami)) return XatoBer("Nasiya_XatoAloqa", "telefon");
        if (Format.PulOl(Summa) is not > 0) return XatoBer("Nasiya_XatoSumma", "summa");
        if (Muddat is not { } m) return XatoBer("Nasiya_XatoMuddat", "muddat");
        if (DateOnly.FromDateTime(m.Date) < Bugun) return XatoBer("Nasiya_XatoMuddatOtgan", "muddat");
        var izoh = Izoh.Trim();
        return Bajar(() => Malumot.NasiyaYoz(new NasiyaYaratishDto(MijozIsmi.Trim(), telefon, MashinaRaqami.Trim().ToUpperInvariant(),
            Format.PulOl(Summa)!.Value, DateOnly.FromDateTime(m.Date), izoh.Length > 0 ? izoh : null)));
    }
}

// ========================= Qarz qaytdi =========================

public sealed record QarzdorQatori(NasiyaDto N)
{
    public string Harflar => Format.BoshHarflar(N.MijozIsmi);
    public string Qoldiq => Format.Pul(N.Qoldiq);
    public string Izoh => string.Join(" · ", new[] { Format.Telefon(N.Telefon), N.MashinaRaqami }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public partial class QaytishDialogVM : DialogVM
{
    public ObservableCollection<QarzdorQatori> Topilganlar { get; } = new();

    [ObservableProperty] private string _qidiruv = "";
    [ObservableProperty] private string _summa = "";
    [ObservableProperty] private string _izoh = "";
    [ObservableProperty] private bool _smenaHisobiga = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsulNaqd), nameof(UsulPlastik), nameof(UsulDepozit))]
    private TolovTuri _usul = TolovTuri.Naqd;

    [ObservableProperty] private NasiyaDto? _tanlangan;

    public string Kontekst => Dialoglar.SmenaMatni;
    public bool UsulNaqd => Usul == TolovTuri.Naqd;
    public bool UsulPlastik => Usul == TolovTuri.Plastik;
    public bool UsulDepozit => Usul == TolovTuri.Depozit;

    public bool TanlanganBor => Tanlangan is not null;
    public bool RoyxatKorinsin => Tanlangan is null;
    public bool TopilmadiKorinsin => Tanlangan is null && Topilganlar.Count == 0;
    public string Harflar => Format.BoshHarflar(Tanlangan?.MijozIsmi ?? "");
    public string TelefonMatn => Format.Telefon(Tanlangan?.Telefon);
    public string Qarz => Format.Pul(Tanlangan?.Summa ?? 0);
    public string Qaytgan => Format.Pul(Tanlangan?.Qaytgan ?? 0);
    public string Qoldiq => Format.Pul(Tanlangan?.Qoldiq ?? 0);
    public string MuddatMatn => Tanlangan is { } n ? Format.QisqaSana(n.Muddat) : "";
    public bool MuddatOtgan => Tanlangan?.Holati == NasiyaHolati.MuddatiOtgan;
    public string KunBelgi => Tanlangan is { } n ? NasiyaKun(n.MuddatgachaKun) : "";
    public string ToliqMatn => Til.F("Nasiya_Toliq", Qoldiq);
    public string YozilganSatr => Tanlangan is { } n
        ? Til.F("Nasiya_YozilganSatr", Format.QisqaSana(n.Yozildi.ToLocalTime()), n.SmenaId, n.OperatorIsmi) + (n.Izoh is { Length: > 0 } i ? $" · \"{i}\"" : "")
        : "";

    private long? SummaQ => Format.PulOl(Summa);
    public bool SummaKop => SummaQ > (Tanlangan?.Qoldiq ?? 0);
    public string QoladiganQarz => Tanlangan is { } n && SummaQ is { } s && s <= n.Qoldiq ? Format.Pul(n.Qoldiq - s) : "—";
    public string QoladiganIzoh => Tanlangan is not { } n || SummaQ is not { } s ? Til.T("Nasiya_SummaniYozing")
        : s > n.Qoldiq ? Til.T("Nasiya_QarzdanKop")
        : s == n.Qoldiq ? Til.T("Nasiya_ToliqYopiladi")
        : n.Holati == NasiyaHolati.MuddatiOtgan ? Til.F("Nasiya_MuddatiOtganQoladi", Format.QisqaSana(n.Muddat))
        : Til.F("Nasiya_MuddatigaQoladi", Format.QisqaSana(n.Muddat));

    /// <summary>Boshliq smenadan tashqari ham yopa oladi (smena hisobiga ta'sir qilmaydi) — belgilash faqat unga ko'rinadi.</summary>
    public bool SmenaTanlovKorinsin => Malumot.JoriyFoydalanuvchi.Boshliqmi;

    public static string NasiyaKun(int kun) => kun switch
    {
        < 0 => Til.F("Nasiya_KunOtdi", -kun),
        0 => Til.T("Nasiya_Bugun"),
        1 => Til.T("Nasiya_Ertaga"),
        _ => Til.F("Nasiya_KunQoldi", kun),
    };

    public void Och(NasiyaDto? n)
    {
        _serverNatija = null;
        Qidiruv = Summa = Izoh = Xato = "";
        Usul = TolovTuri.Naqd;
        SmenaHisobiga = Malumot.JoriySmena is not null;
        Tanlangan = n;
        Filtrla();
        OnPropertyChanged(nameof(Kontekst));
        OnPropertyChanged(nameof(SmenaTanlovKorinsin));
        Ochiq = true;
    }

    private readonly Kechiktirgich _qidiruvYuklash;
    private NasiyaDto[]? _serverNatija;
    public QaytishDialogVM() => _qidiruvYuklash = new Kechiktirgich(Qidir, 250);

    /// <summary>Qidiruv bo'sh bo'lsa — keshdagi faol nasiyalar; aks holda GET /nasiyalar?holat=faol&q= (server telefon bo'lagi bilan ham topadi).</summary>
    partial void OnQidiruvChanged(string value)
    {
        _serverNatija = null;
        Filtrla();
        if (value.Trim().Length > 0) _qidiruvYuklash.Rejala();
    }

    private async Task Qidir(Func<bool> dolzarb)
    {
        var q = Qidiruv.Trim();
        if (q.Length == 0) return;
        var d = await Malumot.Api.Nasiyalar("faol", q);
        if (!dolzarb() || q != Qidiruv.Trim() || d is null) return;
        _serverNatija = d.Royxat;
        // Qator bosilib turgan paytda ro'yxat almashsa, tugma qayta yaratiladi va bosish yo'qoladi — natija qo'yib yuborilgach qo'llanadi
        if (_ushlangan) { _kechikkan = true; return; }
        Filtrla();
    }

    private bool _ushlangan, _kechikkan;

    /// <summary>Ko'rinish: ro'yxatdagi qator bosilganda true, qo'yib yuborilganda (bosish ishlangach) false.</summary>
    public void RoyxatniUshla(bool ha)
    {
        _ushlangan = ha;
        if (ha || !_kechikkan) return;
        _kechikkan = false;
        if (Tanlangan is null) Filtrla();
    }
    partial void OnTanlanganChanged(NasiyaDto? value) { Summa = ""; Yangila(); }
    partial void OnSummaChanged(string value) => Yangila();

    private void Yangila()
    {
        foreach (var p in new[] { nameof(TanlanganBor), nameof(RoyxatKorinsin), nameof(TopilmadiKorinsin), nameof(Harflar), nameof(TelefonMatn), nameof(Qarz),
                     nameof(Qaytgan), nameof(Qoldiq), nameof(MuddatMatn), nameof(MuddatOtgan), nameof(KunBelgi), nameof(ToliqMatn),
                     nameof(YozilganSatr), nameof(SummaKop), nameof(QoladiganQarz), nameof(QoladiganIzoh) })
            OnPropertyChanged(p);
    }

    private void Filtrla()
    {
        Topilganlar.Clear();
        var q = Qidiruv.Trim();
        // Server natijasi kelguncha — keshdan xuddi shu qoida bilan (server tartibi saqlanadi).
        var manba = _serverNatija is { } sn ? sn.Where(n => n.Qoldiq > 0)
            : NasiyaQidiruv.Filtrla((Malumot.FaolNasiyalar?.Royxat ?? []).Where(n => n.Qoldiq > 0), q);
        foreach (var n in manba.Take(6))
            Topilganlar.Add(new QarzdorQatori(n));
        OnPropertyChanged(nameof(TopilmadiKorinsin));
    }

    [RelayCommand] private void Tanla(QarzdorQatori q) => Tanlangan = q.N;
    [RelayCommand] private void Boshqa() { Tanlangan = null; Filtrla(); }
    [RelayCommand] private void Toliq() { if (Tanlangan is { } n) Summa = Format.Pul(n.Qoldiq); }
    [RelayCommand] private void UsulTanla(string u) => Usul = Enum.Parse<TolovTuri>(u);

    [RelayCommand]
    private Task Saqla()
    {
        if (Tanlangan is not { } n) { Xato = Til.T("Nasiya_QarzdorniToping"); return Task.CompletedTask; }
        if (SummaQ is not > 0) { Xato = Til.T("Nasiya_SummaniYozing"); return Task.CompletedTask; }
        if (SummaQ > n.Qoldiq) { Xato = Til.T("Nasiya_QarzdanKop"); return Task.CompletedTask; }
        var izoh = Izoh.Trim();
        return Bajar(() => Malumot.QarzQaytdi(n.Id, new NasiyaQaytishiYaratishDto(SummaQ!.Value, Usul,
            SmenaHisobiga && Malumot.JoriySmena is not null, izoh.Length > 0 ? izoh : null)));
    }
}

// ========================= Xarajat =========================

public partial class XarajatDialogVM : DialogVM
{
    [ObservableProperty] private string _summa = "";
    [ObservableProperty] private string _sabab = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Kassa), nameof(Depozit), nameof(ManbaIzoh))]
    private XarajatManbai _manba = XarajatManbai.Kassa;

    public string Kontekst => Til.F("Xarajat_Izoh", Malumot.JoriySmena?.Id ?? 0);
    public bool Kassa => Manba == XarajatManbai.Kassa;
    public bool Depozit => Manba == XarajatManbai.Depozit;
    public string ManbaIzoh => Til.T(Kassa ? "Xarajat_KassaIzoh" : "Xarajat_DepozitIzoh");

    public void Och()
    {
        Summa = Sabab = Xato = "";
        Manba = XarajatManbai.Kassa;
        OnPropertyChanged(nameof(Kontekst));
        Ochiq = true;
    }

    [RelayCommand] private void TezSabab(string kalit) => Sabab = Til.T(kalit);
    [RelayCommand] private void ManbaTanla(string m) => Manba = Enum.Parse<XarajatManbai>(m);

    [RelayCommand]
    private Task Saqla()
    {
        if (Format.PulOl(Summa) is not > 0) { Xato = Til.T("Nasiya_XatoSumma"); return Task.CompletedTask; }
        if (string.IsNullOrWhiteSpace(Sabab)) { Xato = Til.T("Xarajat_XatoSabab"); return Task.CompletedTask; }
        return Bajar(() => Malumot.XarajatYoz(new XarajatYaratishDto(Format.PulOl(Summa)!.Value, Sabab.Trim(), Manba)));
    }
}

// ========================= Bakka kirim =========================

public sealed record AparatTanlovi(Aparat A)
{
    public string Nomi => $"{Til.F("Aparat_Raqami", A.Raqam)} · {A.Yoqilgi.Nomi}";
}

public partial class BakKirimDialogVM : DialogVM
{
    public List<AparatTanlovi> Aparatlar { get; private set; } = new();

    [ObservableProperty] private AparatTanlovi? _aparat;
    [ObservableProperty] private string _litr = "";
    [ObservableProperty] private string _hujjat = "";
    [ObservableProperty] private DateTime? _sana;
    [ObservableProperty] private TimeSpan? _soat;

    public string Hozirgi => Format.ButunLitr(Aparat?.A.BakQoldiq ?? 0);
    public bool HozirgiManfiy => Aparat?.A.BakManfiy == true;
    public string OxirgiKirim => Aparat?.A is { OxirgiKirimVaqti: { } v } a
        ? Til.F("Bak_OxirgiKirim", Format.QisqaSana(v), Format.ButunLitr(a.OxirgiKirimLitr ?? 0)) : Til.T("Bak_KirimYoq");
    private decimal? LitrQ => Format.KasrOl(Litr);
    public string Keyin => Aparat is { } a && LitrQ is > 0 ? Format.ButunLitr(a.A.BakQoldiq + LitrQ.Value) : "—";

    public void Och(Aparat? a)
    {
        Aparatlar = Malumot.Aparatlar.OrderBy(x => x.Raqam).Select(x => new AparatTanlovi(x)).ToList();
        OnPropertyChanged(nameof(Aparatlar));
        Aparat = Aparatlar.FirstOrDefault(x => x.A == a) ?? Aparatlar.FirstOrDefault();
        Litr = Hujjat = Xato = "";
        var hozir = DateTime.Now;
        Sana = hozir.Date;
        Soat = new TimeSpan(hozir.Hour, hozir.Minute, 0);
        Ochiq = true;
    }

    partial void OnAparatChanged(AparatTanlovi? value) => Yangila();
    partial void OnLitrChanged(string value) => Yangila();
    private void Yangila()
    {
        foreach (var p in new[] { nameof(Hozirgi), nameof(HozirgiManfiy), nameof(OxirgiKirim), nameof(Keyin) }) OnPropertyChanged(p);
    }

    [RelayCommand]
    private Task Saqla()
    {
        if (Aparat is not { } a || LitrQ is not > 0) { Xato = Til.T("Bak_Xato"); return Task.CompletedTask; }
        // Kelgan vaqt (mahalliy) → UTC; bo'sh bo'lsa server hozirgi vaqtni oladi.
        DateTime? vaqt = Sana is { } kun ? DateTime.SpecifyKind(kun.Date + (Soat ?? TimeSpan.Zero), DateTimeKind.Local).ToUniversalTime() : null;
        var hujjat = Hujjat.Trim();
        return Bajar(() => Malumot.BakKirim(a.A.Id, new BakKirimYaratishDto(LitrQ!.Value, vaqt, hujjat.Length > 0 ? hujjat : null)));
    }
}

// ========================= Narx o'zgarishi =========================

/// <summary>Narx o'zgarishida aparatning hozirgi pult ko'rsatkichi: shu paytgacha sotilgan litr eski narxda alohida segment bo'ladi.</summary>
public partial class NarxAparatQatori : ObservableObject
{
    public Aparat A { get; }
    public YoqilgiBelgi Yoqilgi { get; }
    public decimal Oldingi { get; }
    /// <summary>Oldingi qiymat smena boshidagimi (true) yoki oxirgi narx o'zgarishida qayd etilganmi (false).</summary>
    public bool SmenaBoshi { get; }
    private readonly long _eskiNarx;
    private readonly Action _ozgardi;

    [ObservableProperty] private string _qiymat = "";

    public NarxAparatQatori(Aparat a, IEnumerable<SmenaKorsatkichDto> qayd, long eskiNarx, Action ozgardi)
    {
        A = a; _eskiNarx = eskiNarx; _ozgardi = ozgardi;
        Yoqilgi = YoqilgiBelgi.Ol(a.Yoqilgi);
        var l = qayd.Where(k => k.AparatId == a.Id).ToList();
        SmenaBoshi = l.Count == 0;
        Oldingi = SmenaHisobi.Oldingi(a, l);
    }

    public string Nomi => Til.F("Aparat_Raqami", A.Raqam);
    public string OldingiYorliq => Til.T(SmenaBoshi ? "Narx_SmenaBoshida" : "Narx_OxirgiQayd");
    public string OldingiMatn => Format.Son(Oldingi);
    public decimal? Q => Format.KasrOl(Qiymat);
    public bool Xato => Q is { } q ? q < Oldingi : Qiymat.Trim().Length > 0;
    public decimal? Litr => Q is { } q && q >= Oldingi ? SmenaHisobi.Litr(Oldingi, q) : null;
    public long? Summa => Litr is { } l ? SmenaHisobi.Summa(l, _eskiNarx) : null;
    public string Izoh => Xato ? Til.T(SmenaBoshi ? "Narx_SmenaBoshidanKichik" : "Narx_OxirgidanKichik")
        : Litr is { } l ? Til.F("Narx_Sotilgan", Format.Son(l), Format.Pul(_eskiNarx), Format.Pul(Summa!.Value))
        : Til.T("Narx_KorsatkichniYozing");

    partial void OnQiymatChanged(string value)
    {
        foreach (var p in new[] { nameof(Q), nameof(Xato), nameof(Litr), nameof(Summa), nameof(Izoh) }) OnPropertyChanged(p);
        _ozgardi();
    }
}

public partial class NarxDialogVM : DialogVM
{
    public ObservableCollection<NarxAparatQatori> Qatorlar { get; } = new();
    public YoqilgiTuri? Yoqilgi { get; private set; }

    [ObservableProperty] private string _yangiNarx = "";

    public string Kontekst => Til.F("Narx_Kontekst", Yoqilgi?.Nomi ?? "");
    public string Hozirgi => Format.Pul(Yoqilgi?.Narx ?? 0);
    public bool SmenaOchiq => Malumot.JoriySmena is not null;
    public bool SmenaYopiq => !SmenaOchiq;
    public string SmenaSarlavha => Til.F("Narx_SmenaOchiq", Malumot.JoriySmena?.Id ?? 0);
    public string SmenaIzoh => Til.F("Narx_SmenaOchiqIzoh", Yoqilgi?.Nomi ?? "");
    public bool AparatYoq => SmenaOchiq && Qatorlar.Count == 0;
    public string EskiJami => Format.Pul(Qatorlar.Sum(q => q.Summa ?? 0));
    public string EskiIzoh => Til.F("Narx_JamiLitr", Format.Son(Qatorlar.Sum(q => q.Litr ?? 0)), Hozirgi);
    public string IkkiQism => Til.F("Narx_IkkiQism", Hozirgi);

    /// <summary>Saqlangandan keyin chaqiriladi (Sozlamalar ro'yxatini yangilash uchun).</summary>
    public event Action? Saqlandi;

    public void Och(YoqilgiTuri y)
    {
        Yoqilgi = y;
        YangiNarx = Format.Pul(y.Narx);
        Xato = "";
        Qatorlar.Clear();
        if (SmenaOchiq)
        {
            var qayd = Malumot.Joriy?.Korsatkichlar ?? [];
            foreach (var a in Malumot.Aparatlar.Where(a => a.Yoqilgi.Id == y.Id).OrderBy(a => a.Raqam))
                Qatorlar.Add(new NarxAparatQatori(a, qayd, y.Narx, Yangila));
        }
        OnPropertyChanged(string.Empty);
        Ochiq = true;
    }

    private void Yangila()
    {
        OnPropertyChanged(nameof(EskiJami));
        OnPropertyChanged(nameof(EskiIzoh));
    }

    [RelayCommand]
    private Task Saqla()
    {
        if (Yoqilgi is not { } y) return Task.CompletedTask;
        if (Format.PulOl(YangiNarx) is not > 0) { Xato = Til.T("Nasiya_XatoSumma"); return Task.CompletedTask; }
        var narx = Format.PulOl(YangiNarx)!.Value;
        AparatKorsatkichDto[]? korsatkichlar = null;
        if (SmenaOchiq && narx != y.Narx)
        {
            if (Qatorlar.Any(q => q.Q is null || q.Xato)) { Xato = Til.T("Narx_KorsatkichlarniTogri"); return Task.CompletedTask; }
            korsatkichlar = Qatorlar.Select(q => new AparatKorsatkichDto(q.A.Id, q.Q!.Value)).ToArray();
        }
        return Bajar(async () =>
        {
            await Malumot.YoqilgiTahrirla(y.Id, new YoqilgiTahrirlashDto(y.Nomi, narx, y.Rang, korsatkichlar));
            Saqlandi?.Invoke();
        });
    }
}
