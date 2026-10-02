using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FuelControl.Contracts.Dto;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Smenalar ro'yxati, smena ochish va yopish (naqd/plastik/click alohida solishtiriladi).</summary>
public partial class SmenalarViewModel : ObservableObject
{
    public ObservableCollection<Smena> Smenalar { get; } = new();
    public List<Foydalanuvchi> OperatorFiltri { get; private set; } = new();

    [ObservableProperty] private Foydalanuvchi? _tanlanganOperator;
    [ObservableProperty] private int _davrIndeks; // 0 bugun, 1 hafta, 2 oy, 3 hammasi

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TanlanganBormi), nameof(YopishMumkin), nameof(YopishSarlavha), nameof(YopishIzohMatni))]
    private Smena? _tanlangan;

    /// <summary>Tanlangan smena sotuvlari — serverdan (keshda faqat bugungi sotuvlar bor).</summary>
    [ObservableProperty] private List<Sotuv> _tanlanganSotuvlar = new();

    private readonly KechiktirilganIsh _sotuvlarniYuklash;
    private readonly KechiktirilganIsh _hammasiniYuklash;
    private List<Smena>? _hammasi; // "Hammasi" filtri uchun serverdan olingan to'liq ro'yxat

    // Yopish paneli
    [ObservableProperty] private bool _yopishOchiq;
    [ObservableProperty] private string _naqd = "";
    [ObservableProperty] private string _plastik = "";
    [ObservableProperty] private string _click = "";
    [ObservableProperty] private string _izoh = "";

    private static Foydalanuvchi Joriy => Malumot.JoriyFoydalanuvchi;
    private static bool Operatormi => Joriy.Rol == Rol.Operator;

    public bool TanlanganBormi => Tanlangan is not null;

    /// <summary>Yopish ruxsati bor va smena ochiq; operator faqat o'zinikini yopadi.</summary>
    public bool YopishMumkin => Tanlangan?.Ochiqmi == true && Joriy.Bor(Ruxsat.SmenaYopish) &&
                                (!Operatormi || Tanlangan.Operator.Id == Joriy.Id);

    /// <summary>Ochish ruxsati bor va joriy foydalanuvchining ochiq smenasi yo'q.</summary>
    public bool OchishMumkin => Joriy.Bor(Ruxsat.SmenaOchish) && JoriySmena is null;
    public Smena? JoriySmena => Malumot.Smenalar.FirstOrDefault(s => s.Ochiqmi && s.Operator.Id == Joriy.Id);
    public bool JoriySmenaBor => JoriySmena is not null;
    public bool JoriySmenaniYopishMumkin => JoriySmenaBor && Joriy.Bor(Ruxsat.SmenaYopish);
    public string JoriySmenaMatni => JoriySmena is { } s
        ? Til.F("SmenaHolati", s.Id, Format.Vaqt(s.Boshlandi), s.SotuvSoni, Format.Som(s.KutilganJami))
        : Til.T("SmenaOchilmagan");
    public string YopishSarlavha => Til.F("SmenaYopishSarlavha", Tanlangan?.Id ?? 0);
    public string YopishIzohMatni => Til.F("SmenaYopishIzoh", Tanlangan?.Operator.ToliqIsm ?? "");
    public bool OperatorFiltriKorinsin => !Operatormi;

    public long NaqdFarq => Raqam(Naqd) - (Tanlangan?.KutilganNaqd ?? 0);
    public long PlastikFarq => Raqam(Plastik) - (Tanlangan?.KutilganPlastik ?? 0);
    public long ClickFarq => Raqam(Click) - (Tanlangan?.KutilganClick ?? 0);
    public long JamiFarq => NaqdFarq + PlastikFarq + ClickFarq;
    public string NaqdFarqMatni => Format.Farq(NaqdFarq);
    public string PlastikFarqMatni => Format.Farq(PlastikFarq);
    public string ClickFarqMatni => Format.Farq(ClickFarq);
    public string JamiFarqMatni => Format.Farq(JamiFarq);
    public bool KamomatBor => JamiFarq < 0;
    public bool OrtiqchaBor => JamiFarq > 0;
    public string YakunIzohi => JamiFarq switch
    {
        < 0 => Til.F("KamomatYozildi", Format.Som(-JamiFarq)),
        > 0 => Til.F("OrtiqchaYozildi", Format.Som(JamiFarq)),
        _ => Til.T("FarqYoq"),
    };

    public string OchiqSoni => Malumot.Smenalar.Count(s => s.Ochiqmi).ToString();
    public string OyKamomat => Format.Pul(Malumot.Smenalar.Where(s => s.Boshlandi.Month == DateTime.Today.Month).Sum(s => s.Kamomat));
    public string OySmenalar => Malumot.Smenalar.Count(s => s.Boshlandi.Month == DateTime.Today.Month).ToString();

    public SmenalarViewModel()
    {
        _sotuvlarniYuklash = new KechiktirilganIsh(SotuvlarniYukla, 150);
        _hammasiniYuklash = new KechiktirilganIsh(HammasiniYukla, 150);
        OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("BarchaOperatorlar") }, .. Malumot.Operatorlar];
        TanlanganOperator = OperatorFiltri[0];
        DavrIndeks = 1;
        Filtrla();
        Malumot.Ozgardi += Yangila;
        Malumot.AloqaOzgardi += () =>
        {
            SmenaOchCommand.NotifyCanExecuteChanged();
            SmenaniYopCommand.NotifyCanExecuteChanged();
            YopishniBoshlaCommand.NotifyCanExecuteChanged();
            JoriySmenaniYopCommand.NotifyCanExecuteChanged();
        };
        Til.Ozgardi += () =>
        {
            OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("BarchaOperatorlar") }, .. Malumot.Operatorlar];
            OnPropertyChanged(string.Empty);
            TanlanganOperator = null;
            TanlanganOperator = OperatorFiltri[0];
        };
    }

    /// <summary>Login yoki boshqa sahifadagi o'zgarishdan keyin hammasini qayta hisoblash.</summary>
    private void Yangila()
    {
        // Operatorlar ro'yxati login'dan keyin keladi yoki o'zgaradi — filtrni qayta quramiz (tanlov Id bo'yicha saqlanadi).
        if (!OperatorFiltri.Skip(1).Select(o => o.Id).SequenceEqual(Malumot.Operatorlar.Select(o => o.Id)))
        {
            var tanlanganId = TanlanganOperator?.Id ?? 0;
            OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("BarchaOperatorlar") }, .. Malumot.Operatorlar];
            OnPropertyChanged(nameof(OperatorFiltri));
            TanlanganOperator = OperatorFiltri.FirstOrDefault(o => o.Id == tanlanganId) ?? OperatorFiltri[0];
        }
        if (!Malumot.Kirilgan) { _hammasi = null; TanlanganSotuvlar = new(); }
        else if (DavrIndeks == 3) _hammasiniYuklash.Rejala();
        Filtrla();
        if (Malumot.Kirilgan && Tanlangan is not null) _sotuvlarniYuklash.Rejala();
        foreach (var n in new[] { nameof(OchishMumkin), nameof(YopishMumkin), nameof(JoriySmena), nameof(JoriySmenaBor),
                     nameof(JoriySmenaniYopishMumkin), nameof(JoriySmenaMatni), nameof(OperatorFiltriKorinsin),
                     nameof(OchiqSoni), nameof(OyKamomat), nameof(OySmenalar) })
            OnPropertyChanged(n);
    }

    partial void OnTanlanganChanged(Smena? value)
    {
        TanlanganSotuvlar = new();
        if (value is not null && Malumot.Kirilgan) _sotuvlarniYuklash.Rejala();
    }

    private async Task SotuvlarniYukla(Func<bool> dolzarb)
    {
        if (Tanlangan is not { } s) return;
        var royxat = await Malumot.SmenaSotuvlari(s.Id);
        if (dolzarb() && Tanlangan == s) TanlanganSotuvlar = royxat;
    }

    private async Task HammasiniYukla(Func<bool> dolzarb)
    {
        int? opId = Operatormi ? Joriy.Id : TanlanganOperator is { Id: > 0 } o ? o.Id : null;
        var royxat = await Malumot.BarchaSmenalar(opId);
        if (!dolzarb() || DavrIndeks != 3) return;
        _hammasi = royxat;
        Filtrla();
    }

    partial void OnTanlanganOperatorChanged(Foydalanuvchi? value)
    {
        if (DavrIndeks == 3 && Malumot.Kirilgan) _hammasiniYuklash.Rejala();
        Filtrla();
    }

    partial void OnDavrIndeksChanged(int value)
    {
        if (value == 3 && Malumot.Kirilgan) _hammasiniYuklash.Rejala();
        Filtrla();
    }
    partial void OnNaqdChanged(string value) => FarqYangila();
    partial void OnPlastikChanged(string value) => FarqYangila();
    partial void OnClickChanged(string value) => FarqYangila();

    /// <summary>Bugun/hafta/oy — keshdan (oxirgi 60 kun); "Hammasi" — serverdan olingan ro'yxat (kelguncha kesh ko'rinadi).</summary>
    private void Filtrla()
    {
        var dan = DavrIndeks switch
        {
            0 => DateTime.Today,
            1 => DateTime.Today.AddDays(-7),
            2 => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1),
            _ => DateTime.MinValue,
        };
        var manba = DavrIndeks == 3 && _hammasi is not null ? _hammasi : Malumot.Smenalar;
        var t = Tanlangan;
        Smenalar.Clear();
        foreach (var s in manba.Where(s => s.Boshlandi >= dan &&
                     (Operatormi ? s.Operator.Id == Joriy.Id
                                 : TanlanganOperator is null || TanlanganOperator.Id == 0 || s.Operator.Id == TanlanganOperator.Id)))
            Smenalar.Add(s);
        var yangi = Smenalar.FirstOrDefault(s => s.Id == t?.Id) ?? Smenalar.FirstOrDefault();
        if (yangi != Tanlangan) Tanlangan = yangi;
    }

    [RelayCommand]
    private void DavrniTanla(string i) => DavrIndeks = int.Parse(i);

    private static bool AloqaBor() => Malumot.AloqaBor;

    /// <summary>Joriy foydalanuvchi uchun yangi smena ochish (serverda).</summary>
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task SmenaOch()
    {
        if (!OchishMumkin) return;
        try
        {
            var smena = await Malumot.SmenaOch();
            Tanlangan = Smenalar.FirstOrDefault(s => s.Id == smena.Id);
        }
        catch (ApiXatosi e)
        {
            Bildirish.Xato(e.Message);
        }
    }

    /// <summary>Tanlangan smenani yopish dialogini ochish.</summary>
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private void YopishniBoshla()
    {
        if (Tanlangan is null || !YopishMumkin) return;
        Naqd = ""; Plastik = Format.Pul(Tanlangan.KutilganPlastik); Click = Format.Pul(Tanlangan.KutilganClick); Izoh = "";
        YopishOchiq = true;
        FarqYangila();
    }

    /// <summary>Sotuv sahifasidan: joriy foydalanuvchining ochiq smenasini yopish.</summary>
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private void JoriySmenaniYop()
    {
        if (JoriySmena is null) return;
        Tanlangan = JoriySmena;
        YopishniBoshla();
    }

    [RelayCommand]
    private void YopishniBekorQil() => YopishOchiq = false;

    /// <summary>Server yopadi: kutilgan summani qayta hisoblaydi, farqni kamomat/ortiqcha sifatida operator hisobiga yozadi.</summary>
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task SmenaniYop()
    {
        if (Tanlangan is null) return;
        try
        {
            var izoh = Izoh.Trim();
            await Malumot.SmenaYop(Tanlangan.Id, new SmenaYopishDto(Raqam(Naqd), Raqam(Plastik), Raqam(Click), izoh.Length > 0 ? izoh : null));
            YopishOchiq = false;
        }
        catch (ApiXatosi e)
        {
            Bildirish.Xato(e.Message);
        }
    }

    private void FarqYangila()
    {
        foreach (var n in new[] { nameof(NaqdFarqMatni), nameof(PlastikFarqMatni), nameof(ClickFarqMatni), nameof(JamiFarqMatni),
                     nameof(KamomatBor), nameof(OrtiqchaBor), nameof(YakunIzohi), nameof(NaqdFarq), nameof(PlastikFarq), nameof(ClickFarq) })
            OnPropertyChanged(n);
    }

    private static long Raqam(string s)
    {
        var t = new string(s.Where(char.IsDigit).ToArray());
        return long.TryParse(t, out var v) ? v : 0;
    }
}
