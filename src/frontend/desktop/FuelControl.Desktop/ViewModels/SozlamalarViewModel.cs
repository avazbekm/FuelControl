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

public partial class YoqilgiQatoriVm : ObservableObject
{
    public YoqilgiTuri Yoqilgi { get; }
    public string Nomi => Yoqilgi.Nomi;
    public string Rang => Yoqilgi.Rang;
    public string JoriyNarx => Format.Pul(Yoqilgi.Narx);
    public string AparatSoni => Malumot.Aparatlar.Count(a => a.Yoqilgi.Id == Yoqilgi.Id) + " " + Til.T("TaAparat");
    [ObservableProperty] private string _yangiNarx = "";
    public YoqilgiQatoriVm(YoqilgiTuri y)
    {
        Yoqilgi = y;
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
        // YangiNarx (kiritilayotgan matn) tegilmaydi — faqat ko'rsatiladigan qiymatlar yangilanadi.
        Malumot.Ozgardi += () => { OnPropertyChanged(nameof(Nomi)); OnPropertyChanged(nameof(Rang)); OnPropertyChanged(nameof(JoriyNarx)); OnPropertyChanged(nameof(AparatSoni)); };
    }
    public void NarxYangilandi() => OnPropertyChanged(nameof(JoriyNarx));
}

/// <summary>Bitta ruxsat qatori: belgilansa server'ga darhol yoziladi; rad etilsa belgi qaytariladi.</summary>
public partial class RuxsatElementi : ObservableObject
{
    private readonly Foydalanuvchi _f;
    private bool _qaytarilmoqda;
    public Ruxsat Ruxsat { get; }
    public string Nomi => Til.T("R_" + Ruxsat);
    public string Izoh => Til.T("RI_" + Ruxsat);
    public string Guruh { get; }
    [ObservableProperty] private bool _tanlangan;

    public RuxsatElementi(Foydalanuvchi f, (Ruxsat Ruxsat, string Guruh) r)
    {
        _f = f; Ruxsat = r.Ruxsat; Guruh = r.Guruh;
        _tanlangan = f.Bor(r.Ruxsat);
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
    }

    partial void OnTanlanganChanged(bool value)
    {
        if (_qaytarilmoqda) return;
        _ = Saqla(value);
    }

    private async Task Saqla(bool value)
    {
        var eski = _f.Ruxsatlar;
        var yangi = eski.ToHashSet();
        if (value) yangi.Add(Ruxsat); else yangi.Remove(Ruxsat);
        _f.Ruxsatlar = yangi;
        try
        {
            if (!Malumot.AloqaBor) throw new ApiXatosi(Til.T("AloqaYoq"), 0);
            await Malumot.RuxsatlarniOrnat(_f.Id, yangi);
        }
        catch (ApiXatosi e)
        {
            _f.Ruxsatlar = eski;
            _qaytarilmoqda = true;
            Tanlangan = !value;
            _qaytarilmoqda = false;
            Bildirish.Xato(e.Message);
        }
    }
}

/// <summary>Rang tanlash chipi.</summary>
public partial class RangElementi : ObservableObject
{
    public string Rang { get; init; } = "";
    [ObservableProperty] private bool _tanlangan;
}

/// <summary>ComboBox uchun rol elementi (nomi tilga qarab).</summary>
public sealed class RolElementi
{
    public Rol Rol { get; init; }
    public string Nomi => Til.T("Rol_" + Rol);
    public override string ToString() => Nomi;
}

/// <summary>Sozlamalar (Admin): narxlar, aparatlar, foydalanuvchilar, ruxsatlar, zaxira. Hamma o'zgarish server orqali.</summary>
public partial class SozlamalarViewModel : ObservableObject
{
    public ObservableCollection<YoqilgiQatoriVm> Yoqilgilar { get; } = new();
    public ObservableCollection<YoqilgiTuri> YoqilgiTurlari { get; } = new();

    // ---- Yoqilg'i dialogi
    public static readonly string[] Palitra = ["#2F6BFF", "#7C5CFF", "#E8A317", "#1EA66A", "#19B5C9", "#E5484D", "#F0668A", "#5A6B88"];
    public List<RangElementi> Ranglar { get; } = Palitra.Select(r => new RangElementi { Rang = r }).ToList();
    [ObservableProperty] private bool _yoqilgiDialogOchiq;
    [ObservableProperty] private YoqilgiTuri? _yoqilgiTahrir;
    [ObservableProperty] private string _yNomi = "";
    [ObservableProperty] private string _yNarx = "";
    [ObservableProperty] private string _yRang = "#2F6BFF";
    [ObservableProperty] private string _yXato = "";
    public string YoqilgiDialogSarlavha => YoqilgiTahrir is null ? Til.T("YangiYoqilgi") : Til.T("YoqilginiTahrirlash");
    public bool YoqilgiYangi => YoqilgiTahrir is null;
    public bool YoqilginiOchirishMumkin => YoqilgiTahrir is not null && !Malumot.Aparatlar.Any(a => a.Yoqilgi.Id == YoqilgiTahrir.Id);
    public ObservableCollection<Aparat> Aparatlar { get; } = new();
    public ObservableCollection<Foydalanuvchi> Foydalanuvchilar { get; } = new();
    public ObservableCollection<NarxTarixi> NarxTarixi { get; } = new();
    public List<RolElementi> Rollar { get; private set; } = Enum.GetValues<Rol>().Select(r => new RolElementi { Rol = r }).ToList();

    [ObservableProperty] private int _bolim; // 0 narxlar, 1 aparatlar, 2 foydalanuvchilar, 3 ruxsatlar, 4 zaxira
    public bool NarxlarBolimi => Bolim == 0;
    public bool AparatlarBolimi => Bolim == 1;
    public bool FoydalanuvchilarBolimi => Bolim == 2;
    public bool RuxsatlarBolimi => Bolim == 3;
    public bool ZaxiraBolimi => Bolim == 4;

    // ---- Ruxsatlar bo'limi
    [ObservableProperty] private Foydalanuvchi? _ruxsatFoydalanuvchi;
    public ObservableCollection<RuxsatElementi> BolimRuxsatlari { get; } = new();
    public ObservableCollection<RuxsatElementi> AmalRuxsatlari { get; } = new();
    public string RuxsatIzohi => RuxsatFoydalanuvchi is null ? "" :
        $"{RuxsatFoydalanuvchi.RolNomi} · {RuxsatFoydalanuvchi.RuxsatSoni} {Til.T("TaRuxsat")}";

    // ---- Aparat dialogi
    [ObservableProperty] private bool _aparatDialogOchiq;
    [ObservableProperty] private Aparat? _aparatTahrir;
    [ObservableProperty] private string _aparatRaqam = "";
    [ObservableProperty] private YoqilgiTuri? _aparatYoqilgi;
    [ObservableProperty] private string _aparatTotalLitr = "";
    [ObservableProperty] private string _aparatXato = "";
    public string AparatDialogSarlavha => AparatTahrir is null ? Til.T("YangiAparat") : Til.T("AparatniTahrirlash");

    // ---- Foydalanuvchi dialogi
    [ObservableProperty] private bool _fDialogOchiq;
    [ObservableProperty] private Foydalanuvchi? _fTahrir;
    [ObservableProperty] private string _fIsm = "";
    [ObservableProperty] private string _fLogin = "";
    [ObservableProperty] private RolElementi? _fRol;
    [ObservableProperty] private string _fMaosh = "";
    [ObservableProperty] private bool _fFaol = true;
    [ObservableProperty] private string _fPinParol = "";
    [ObservableProperty] private string _fXato = "";
    public string FDialogSarlavha => FTahrir is null ? Til.T("YangiFoydalanuvchi") : Til.T("FoydalanuvchiniTahrirlash");
    public bool FYangi => FTahrir is null;

    // ---- PIN/parol tiklash dialogi
    [ObservableProperty] private bool _tiklashOchiq;
    [ObservableProperty] private Foydalanuvchi? _tiklashF;
    [ObservableProperty] private string _yangiPin = "";

    // ---- Zaxira
    [ObservableProperty] private string _oxirgiNusxa = "";

    private string _yoqilgiImzo = "", _aparatImzo = "", _foydalanuvchiImzo = "", _narxImzo = "";

    public SozlamalarViewModel()
    {
        OxirgiNusxa = Til.T("OxirgiNusxaIzoh");
        Sinxronla();
        Malumot.Ozgardi += Sinxronla;
        Malumot.AloqaOzgardi += () =>
        {
            foreach (var c in new IRelayCommand[] { NarxniSaqlaCommand, YoqilginiSaqlaCommand, YoqilginiOchirCommand, AparatniSaqlaCommand,
                         FoydalanuvchiniSaqlaCommand, StandartRuxsatlarCommand, TiklashniSaqlaCommand, HozirNusxaCommand })
                c.NotifyCanExecuteChanged();
        };
        Til.Ozgardi += () =>
        {
            Rollar = Enum.GetValues<Rol>().Select(r => new RolElementi { Rol = r }).ToList();
            OnPropertyChanged(string.Empty);
        };
    }

    private static bool AloqaBor() => Malumot.AloqaBor;

    /// <summary>Kesh o'zgarganda ro'yxatlarni yangilaydi — faqat haqiqatan o'zgarganini (kiritilayotgan matn va tanlov saqlanadi).</summary>
    private void Sinxronla()
    {
        var yImzo = string.Join("|", Malumot.Yoqilgilar.Select(y => $"{y.Id}:{y.Nomi}:{y.Narx}:{y.Rang}"));
        if (yImzo != _yoqilgiImzo)
        {
            var yIdlar = string.Join(",", Malumot.Yoqilgilar.Select(y => y.Id));
            if (yIdlar != string.Join(",", Yoqilgilar.Select(q => q.Yoqilgi.Id)))
            {
                Yoqilgilar.Clear();
                foreach (var y in Malumot.Yoqilgilar) Yoqilgilar.Add(new YoqilgiQatoriVm(y));
            }
            var tanlangan = AparatYoqilgi?.Id;
            YoqilgiTurlari.Clear();
            foreach (var y in Malumot.Yoqilgilar) YoqilgiTurlari.Add(y);
            if (tanlangan is not null) AparatYoqilgi = YoqilgiTurlari.FirstOrDefault(y => y.Id == tanlangan);
            _yoqilgiImzo = yImzo;
        }

        var aImzo = string.Join("|", Malumot.Aparatlar.Select(a => $"{a.Id}:{a.Raqam}:{a.Yoqilgi.Id}:{a.Yoqilgi.Nomi}:{a.TotalLitr}"));
        if (aImzo != _aparatImzo)
        {
            Aparatlar.Clear();
            foreach (var a in Malumot.Aparatlar.OrderBy(a => a.Raqam)) Aparatlar.Add(a);
            _aparatImzo = aImzo;
            OnPropertyChanged(nameof(YoqilginiOchirishMumkin));
        }

        var fImzo = string.Join("|", Malumot.Foydalanuvchilar.Select(f => $"{f.Id}:{f.ToliqIsm}:{f.Login}:{f.Rol}:{f.Faol}:{f.OylikMaosh}:{f.RuxsatSoni}"));
        if (fImzo != _foydalanuvchiImzo)
        {
            Foydalanuvchilar.Clear();
            foreach (var f in Malumot.Foydalanuvchilar) Foydalanuvchilar.Add(f);
            _foydalanuvchiImzo = fImzo;
        }

        var nImzo = $"{Malumot.NarxTarixi.Count}:{Malumot.NarxTarixi.FirstOrDefault()?.Vaqt:O}";
        if (nImzo != _narxImzo)
        {
            NarxTarixi.Clear();
            foreach (var n in Malumot.NarxTarixi) NarxTarixi.Add(n);
            _narxImzo = nImzo;
        }

        // Ruxsatlar bo'limi: tanlangan foydalanuvchini Id bo'yicha saqlab, belgilar serverdagidan farq qilsa qayta quramiz.
        var rf = Foydalanuvchilar.FirstOrDefault(f => f.Id == RuxsatFoydalanuvchi?.Id)
                 ?? Foydalanuvchilar.FirstOrDefault(f => f.Rol == Rol.Operator) ?? Foydalanuvchilar.FirstOrDefault();
        var belgilar = BolimRuxsatlari.Concat(AmalRuxsatlari).Where(e => e.Tanlangan).Select(e => e.Ruxsat).ToHashSet();
        if (rf != RuxsatFoydalanuvchi || (rf is not null && !belgilar.SetEquals(rf.Ruxsatlar)))
        {
            RuxsatFoydalanuvchi = null;
            RuxsatFoydalanuvchi = rf;
        }
        else OnPropertyChanged(nameof(RuxsatIzohi));
    }

    partial void OnBolimChanged(int value)
    {
        foreach (var n in new[] { nameof(NarxlarBolimi), nameof(AparatlarBolimi), nameof(FoydalanuvchilarBolimi), nameof(RuxsatlarBolimi), nameof(ZaxiraBolimi) })
            OnPropertyChanged(n);
    }

    partial void OnRuxsatFoydalanuvchiChanged(Foydalanuvchi? value)
    {
        BolimRuxsatlari.Clear();
        AmalRuxsatlari.Clear();
        if (value is null) return;
        foreach (var r in Ruxsatlar.Royxat)
        {
            var e = new RuxsatElementi(value, r);
            e.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RuxsatIzohi));
            (r.Guruh == "B" ? BolimRuxsatlari : AmalRuxsatlari).Add(e);
        }
        OnPropertyChanged(nameof(RuxsatIzohi));
    }

    [RelayCommand] private void BolimniTanla(string i) => Bolim = int.Parse(i);
    [RelayCommand] private void RuxsatFoydalanuvchisiniTanla(Foydalanuvchi f) => RuxsatFoydalanuvchi = f;

    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task StandartRuxsatlar()
    {
        if (RuxsatFoydalanuvchi is null) return;
        try { await Malumot.RuxsatlarniOrnat(RuxsatFoydalanuvchi.Id, Ruxsatlar.Standart(RuxsatFoydalanuvchi.Rol)); }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    // ================= Narxlar =================
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task NarxniSaqla(YoqilgiQatoriVm q)
    {
        var yangi = long.TryParse(new string(q.YangiNarx.Where(char.IsDigit).ToArray()), out var v) ? v : 0;
        if (yangi <= 0 || yangi == q.Yoqilgi.Narx) return;
        try
        {
            await Malumot.YoqilgiTahrirla(q.Yoqilgi.Id, new YoqilgiTahrirlashDto(q.Yoqilgi.Nomi, yangi, q.Yoqilgi.Rang));
            q.YangiNarx = "";
            q.NarxYangilandi();
        }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    // ================= Yoqilg'i turlari =================
    private void YoqilgiDialogniTayyorla(YoqilgiTuri? y)
    {
        YoqilgiTahrir = y;
        YNomi = y?.Nomi ?? "";
        YNarx = y is null ? "" : Format.Pul(y.Narx);
        YRangniTanla(y?.Rang ?? Palitra.FirstOrDefault(r => !Malumot.Yoqilgilar.Any(x => x.Rang == r)) ?? Palitra[0]);
        YXato = "";
        OnPropertyChanged(nameof(YoqilgiDialogSarlavha)); OnPropertyChanged(nameof(YoqilgiYangi)); OnPropertyChanged(nameof(YoqilginiOchirishMumkin));
        YoqilgiDialogOchiq = true;
    }

    [RelayCommand] private void YoqilgiQosh() => YoqilgiDialogniTayyorla(null);
    [RelayCommand] private void YoqilginiTahrirla(YoqilgiQatoriVm q) => YoqilgiDialogniTayyorla(q.Yoqilgi);
    [RelayCommand] private void YoqilgiDialogniYop() => YoqilgiDialogOchiq = false;

    [RelayCommand]
    private void YRangniTanla(string rang)
    {
        YRang = rang;
        foreach (var r in Ranglar) r.Tanlangan = r.Rang == rang;
    }

    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task YoqilginiSaqla()
    {
        var nomi = YNomi.Trim();
        var narx = long.TryParse(new string(YNarx.Where(char.IsDigit).ToArray()), out var n) ? n : 0;
        if (nomi.Length == 0 || narx <= 0) { YXato = Til.T("Xato_Maydon"); return; }
        if (Malumot.Yoqilgilar.Any(y => y.Nomi.Equals(nomi, StringComparison.OrdinalIgnoreCase) && y.Id != YoqilgiTahrir?.Id)) { YXato = Til.T("Xato_YoqilgiBand"); return; }

        try
        {
            if (YoqilgiTahrir is null) await Malumot.YoqilgiYarat(new YoqilgiYaratishDto(nomi, narx, YRang));
            else await Malumot.YoqilgiTahrirla(YoqilgiTahrir.Id, new YoqilgiTahrirlashDto(nomi, narx, YRang));
            YoqilgiDialogOchiq = false;
        }
        catch (ApiXatosi e) { YXato = e.Message; }
    }

    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task YoqilginiOchir()
    {
        if (YoqilgiTahrir is null) return;
        if (!YoqilginiOchirishMumkin) { YXato = Til.T("Xato_YoqilgiIshlatilmoqda"); return; }
        try
        {
            await Malumot.YoqilgiOchir(YoqilgiTahrir.Id);
            YoqilgiDialogOchiq = false;
        }
        catch (ApiXatosi e) { YXato = e.Message; }
    }

    // ================= Aparatlar =================
    [RelayCommand]
    private void AparatQosh()
    {
        AparatTahrir = null;
        AparatRaqam = (Malumot.Aparatlar.Count == 0 ? 1 : Malumot.Aparatlar.Max(a => a.Raqam) + 1).ToString();
        AparatYoqilgi = YoqilgiTurlari.FirstOrDefault();
        AparatTotalLitr = "";
        AparatXato = "";
        OnPropertyChanged(nameof(AparatDialogSarlavha));
        AparatDialogOchiq = true;
    }

    [RelayCommand]
    private void AparatniTahrirla(Aparat a)
    {
        AparatTahrir = a;
        AparatRaqam = a.Raqam.ToString();
        AparatYoqilgi = YoqilgiTurlari.FirstOrDefault(y => y.Id == a.Yoqilgi.Id);
        AparatTotalLitr = a.TotalLitr.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        AparatXato = "";
        OnPropertyChanged(nameof(AparatDialogSarlavha));
        AparatDialogOchiq = true;
    }

    [RelayCommand] private void AparatDialogniYop() => AparatDialogOchiq = false;

    /// <summary>Tahrirda totalizator qiymati o'zgargan bo'lsa — server uni tuzatadi va "Totalizator tuzatildi" deb auditga yozadi.</summary>
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task AparatniSaqla()
    {
        if (!int.TryParse(AparatRaqam.Trim(), out var raqam) || raqam <= 0 || AparatYoqilgi is null) { AparatXato = Til.T("Xato_Maydon"); return; }
        if (Malumot.Aparatlar.Any(a => a.Raqam == raqam && a.Id != AparatTahrir?.Id)) { AparatXato = Til.T("Xato_AparatBand"); return; }
        var litrMatn = AparatTotalLitr.Trim().Replace(" ", "").Replace(',', '.');
        if (litrMatn.Length > 0 && !decimal.TryParse(litrMatn, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)) { AparatXato = Til.T("Xato_Maydon"); return; }
        var totalLitr = litrMatn.Length == 0 ? 0m : decimal.Parse(litrMatn, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            if (AparatTahrir is null) await Malumot.AparatYarat(new AparatYaratishDto(raqam, AparatYoqilgi.Id, totalLitr));
            else await Malumot.AparatTahrirla(AparatTahrir.Id, new AparatTahrirlashDto(raqam, AparatYoqilgi.Id, totalLitr));
            AparatDialogOchiq = false;
        }
        catch (ApiXatosi e) { AparatXato = e.Message; }
    }

    // ================= Foydalanuvchilar =================
    [RelayCommand]
    private void FoydalanuvchiQosh()
    {
        FTahrir = null;
        FIsm = ""; FLogin = ""; FRol = Rollar.First(r => r.Rol == Rol.Operator); FMaosh = ""; FFaol = true; FPinParol = ""; FXato = "";
        OnPropertyChanged(nameof(FDialogSarlavha)); OnPropertyChanged(nameof(FYangi));
        FDialogOchiq = true;
    }

    [RelayCommand]
    private void FoydalanuvchiniTahrirla(Foydalanuvchi f)
    {
        FTahrir = f;
        FIsm = f.ToliqIsm; FLogin = f.Login; FRol = Rollar.First(r => r.Rol == f.Rol);
        FMaosh = f.OylikMaosh > 0 ? Format.Pul(f.OylikMaosh) : ""; FFaol = f.Faol; FPinParol = ""; FXato = "";
        OnPropertyChanged(nameof(FDialogSarlavha)); OnPropertyChanged(nameof(FYangi));
        FDialogOchiq = true;
    }

    [RelayCommand] private void FDialogniYop() => FDialogOchiq = false;

    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task FoydalanuvchiniSaqla()
    {
        var ism = FIsm.Trim(); var login = FLogin.Trim().ToLowerInvariant();
        if (ism.Length == 0 || login.Length == 0 || FRol is null || (FTahrir is null && FPinParol.Trim().Length == 0)) { FXato = Til.T("Xato_Maydon"); return; }
        if (Malumot.Foydalanuvchilar.Any(f => f.Login.Equals(login, StringComparison.OrdinalIgnoreCase) && f.Id != FTahrir?.Id)) { FXato = Til.T("Xato_LoginBand"); return; }
        var maosh = long.TryParse(new string(FMaosh.Where(char.IsDigit).ToArray()), out var m) ? m : 0;

        try
        {
            if (FTahrir is null)
            {
                var yangi = await Malumot.FoydalanuvchiYarat(new FoydalanuvchiYaratishDto(ism, login, FRol.Rol, maosh, FPinParol.Trim()));
                // Yaratish har doim faol foydalanuvchi beradi; nofaol belgilangan bo'lsa — darhol o'chiramiz.
                if (!FFaol) await Malumot.FoydalanuvchiTahrirla(yangi.Id, new FoydalanuvchiTahrirlashDto(ism, FRol.Rol, false, maosh, login), null);
            }
            else
            {
                await Malumot.FoydalanuvchiTahrirla(FTahrir.Id, new FoydalanuvchiTahrirlashDto(ism, FRol.Rol, FFaol, maosh, login), FPinParol);
            }
            FDialogOchiq = false;
        }
        catch (ApiXatosi e) { FXato = e.Message; }
    }

    [RelayCommand]
    private void TiklashniOch(Foydalanuvchi f) { TiklashF = f; YangiPin = ""; TiklashOchiq = true; }

    [RelayCommand] private void TiklashniYop() => TiklashOchiq = false;

    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task TiklashniSaqla()
    {
        if (TiklashF is null || YangiPin.Trim().Length == 0) return;
        try
        {
            await Malumot.PinOrnat(TiklashF.Id, YangiPin.Trim());
            TiklashOchiq = false;
        }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    // ================= Zaxira =================
    /// <summary>Server bazasidan zaxira nusxa (VACUUM INTO) — fayl serverda, /data/zaxira papkasida.</summary>
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task HozirNusxa()
    {
        try
        {
            var z = await Malumot.Zaxira();
            OxirgiNusxa = $"{Til.T("Bugun")} {z.Vaqt.ToLocalTime():HH:mm} · {z.FaylNomi} · {z.HajmBayt / 1024} KB · {Til.T("Muvaffaqiyatli")}";
        }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    [RelayCommand]
    private void PapkaniOch() => Bildirish.Malumot(Til.T("ZaxiraServerda"));
}
