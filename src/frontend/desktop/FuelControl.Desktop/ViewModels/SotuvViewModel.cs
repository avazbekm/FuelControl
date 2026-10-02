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

public partial class AparatElementi : ObservableObject
{
    public Aparat Aparat { get; }
    public string Nomi => $"{Til.T("Aparat")} {Aparat.Raqam}";
    public string Yoqilgi => Aparat.Yoqilgi.Nomi;
    public string Narx => Format.Pul(Aparat.Yoqilgi.Narx) + " " + Til.T("SomL");
    public string Rang => Aparat.Yoqilgi.Rang;
    public string TotalLitr => Aparat.TotalLitr.ToString("#,0.00", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
    [ObservableProperty] private bool _tanlangan;
    public AparatElementi(Aparat a) { Aparat = a; Til.Ozgardi += () => OnPropertyChanged(string.Empty); Malumot.Ozgardi += () => OnPropertyChanged(string.Empty); }
}

/// <summary>Sotuv kiritish: aparat → summa/litr → to'lov → saqlash. Smena birinchi sotuvda o'zi ochiladi.</summary>
public partial class SotuvViewModel : ObservableObject
{
    public List<AparatElementi> Aparatlar { get; private set; } = new();
    public ObservableCollection<Sotuv> BugungiSotuvlar { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AparatTanlangan), nameof(NarxMatni), nameof(YoqilgiNomi))]
    private AparatElementi? _aparat;

    [ObservableProperty] private bool _summaRejimi = true;
    [ObservableProperty] private string _kiritilgan = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TolovNaqd), nameof(TolovPlastik), nameof(TolovClick), nameof(TolovAralash), nameof(AralashKorinsin))]
    private int _tolov; // 0 naqd, 1 plastik, 2 click, 3 aralash

    [ObservableProperty] private string _aralashNaqd = "";
    [ObservableProperty] private string _aralashPlastik = "";
    [ObservableProperty] private string _aralashClick = "";

    [ObservableProperty] private string _xabar = "";
    [ObservableProperty] private bool _xabarXato;

    private static Foydalanuvchi Joriy => Malumot.JoriyFoydalanuvchi;

    /// <summary>Joriy kiritmaning IdempotencyKey'i: aloqa uzilib qayta bosilsa ham sotuv ikki marta yozilmaydi.
    /// Kiritma o'zgarsa (yoki saqlansa) yangisi olinadi.</summary>
    private Guid _kalit = Guid.NewGuid();

    public bool AparatTanlangan => Aparat is not null;
    public string YoqilgiNomi => Aparat?.Yoqilgi ?? "—";
    public string NarxMatni => Aparat is null ? "" : Format.Pul(Aparat.Aparat.Yoqilgi.Narx) + " " + Til.T("SomLitr");

    public bool TolovNaqd => Tolov == 0;
    public bool TolovPlastik => Tolov == 1;
    public bool TolovClick => Tolov == 2;
    public bool TolovAralash => Tolov == 3;
    public bool AralashKorinsin => Tolov == 3;

    // Yaxlitlash server bilan bir xil (AwayFromZero) — aks holda to'lov summasi server hisobiga mos kelmay qoladi.
    public long Summa => SummaRejimi ? Raqam(Kiritilgan) : (long)Math.Round(Litr * Narx, 0, MidpointRounding.AwayFromZero);
    public decimal Litr => SummaRejimi
        ? (Narx == 0 ? 0 : Math.Round((decimal)Raqam(Kiritilgan) / Narx, 2, MidpointRounding.AwayFromZero))
        : Math.Round(Kasr(Kiritilgan), 2, MidpointRounding.AwayFromZero);
    private long Narx => Aparat?.Aparat.Yoqilgi.Narx ?? 0;

    public string SummaMatni => Format.Som(Summa);
    public string LitrMatni => Format.Litr(Litr);
    public string HisobIzohi => SummaRejimi
        ? $"{Format.Pul(Summa)} ÷ {Format.Pul(Narx)} = {Litr:0.00} {Til.T("L")}"
        : $"{Litr:0.00} {Til.T("L")} × {Format.Pul(Narx)} = {Format.Som(Summa)}";

    public long AralashJami => Raqam(AralashNaqd) + Raqam(AralashPlastik) + Raqam(AralashClick);
    public string AralashQoldiq => Format.Farq(Summa - AralashJami);
    public bool AralashTogri => AralashJami == Summa && Summa > 0;

    public string JamiBugun => Format.Pul(BugungiSotuvlar.Where(s => s.Faolmi).Sum(s => s.Summa));

    // Joriy smena holati (sarlavha ostida)
    public Smena? JoriySmena => Malumot.Smenalar.FirstOrDefault(s => s.Ochiqmi && s.Operator.Id == Joriy.Id);
    public bool SmenaOchiq => JoriySmena is not null;
    public string SmenaMatni => JoriySmena is { } s
        ? Til.F("SmenaHolati", s.Id, Format.Vaqt(s.Boshlandi), s.SotuvSoni, Format.Som(s.KutilganJami))
        : Til.T("SmenaOchilmagan");
    public bool SmenaOchishKorinsin => !SmenaOchiq && Joriy.Bor(Ruxsat.SmenaOchish);
    public bool SmenaYopishKorinsin => SmenaOchiq && Joriy.Bor(Ruxsat.SmenaYopish);

    public SotuvViewModel()
    {
        Yukla();
        Malumot.Ozgardi += Yukla;
        Malumot.AloqaOzgardi += () => SaqlashCommand.NotifyCanExecuteChanged();
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
    }

    /// <summary>Login / smena o'zgarganda joriy foydalanuvchining bugungi sotuvlarini qayta yuklash.</summary>
    private void Yukla()
    {
        if (Aparatlar.Count != Malumot.Aparatlar.Count || Aparatlar.Any(a => !Malumot.Aparatlar.Contains(a.Aparat)))
        {
            Aparatlar = Malumot.Aparatlar.OrderBy(a => a.Raqam).Select(a => new AparatElementi(a)).ToList();
            Aparat = null;
            OnPropertyChanged(nameof(Aparatlar));
        }
        // Narx serverda o'zgargan bo'lishi mumkin — hisob qayta chiqsin.
        Yangila();
        BugungiSotuvlar.Clear();
        foreach (var s in Malumot.Sotuvlar.Where(s => s.Vaqt.Date == DateTime.Today && s.Operator.Id == Joriy.Id).Take(12))
            BugungiSotuvlar.Add(s);
        foreach (var n in new[] { nameof(JamiBugun), nameof(JoriySmena), nameof(SmenaOchiq), nameof(SmenaMatni), nameof(SmenaOchishKorinsin), nameof(SmenaYopishKorinsin) })
            OnPropertyChanged(n);
    }

    partial void OnKiritilganChanged(string value) { KalitniYangila(); Yangila(); }
    partial void OnSummaRejimiChanged(bool value) { Kiritilgan = ""; KalitniYangila(); Yangila(); }
    partial void OnAparatChanged(AparatElementi? value) { KalitniYangila(); Yangila(); }
    partial void OnAralashNaqdChanged(string value) { KalitniYangila(); Yangila(); }
    partial void OnAralashPlastikChanged(string value) { KalitniYangila(); Yangila(); }
    partial void OnAralashClickChanged(string value) { KalitniYangila(); Yangila(); }

    private void Yangila()
    {
        foreach (var n in new[] { nameof(Summa), nameof(Litr), nameof(SummaMatni), nameof(LitrMatni), nameof(HisobIzohi),
                     nameof(AralashJami), nameof(AralashQoldiq), nameof(AralashTogri), nameof(NarxMatni) })
            OnPropertyChanged(n);
        SaqlashCommand.NotifyCanExecuteChanged();
    }

    private void KalitniYangila() => _kalit = Guid.NewGuid();

    partial void OnTolovChanged(int value) => KalitniYangila();

    [RelayCommand]
    private void AparatniTanla(AparatElementi a)
    {
        foreach (var x in Aparatlar) x.Tanlangan = x == a;
        Aparat = a;
    }

    [RelayCommand] private void RejimniTanla(string rejim) => SummaRejimi = rejim == "summa";
    [RelayCommand] private void TolovniTanla(string t) => Tolov = int.Parse(t);
    [RelayCommand] private void Tez(string summa) => Kiritilgan = (Raqam(Kiritilgan) + long.Parse(summa)).ToString();

    private bool SaqlashMumkin() => Malumot.AloqaBor && Aparat is not null && Summa > 0 && (Tolov != 3 || AralashTogri);

    /// <summary>Server sotuvni yozadi: smena yo'q bo'lsa o'zi ochadi, totalizator va smena yakunini hisoblaydi.</summary>
    [RelayCommand(CanExecute = nameof(SaqlashMumkin))]
    private async Task Saqlash()
    {
        var tolovlar = new List<TolovDto>();
        switch (Tolov)
        {
            case 0: tolovlar.Add(new(TolovTuri.Naqd, Summa)); break;
            case 1: tolovlar.Add(new(TolovTuri.Plastik, Summa)); break;
            case 2: tolovlar.Add(new(TolovTuri.Click, Summa)); break;
            default:
                if (Raqam(AralashNaqd) > 0) tolovlar.Add(new(TolovTuri.Naqd, Raqam(AralashNaqd)));
                if (Raqam(AralashPlastik) > 0) tolovlar.Add(new(TolovTuri.Plastik, Raqam(AralashPlastik)));
                if (Raqam(AralashClick) > 0) tolovlar.Add(new(TolovTuri.Click, Raqam(AralashClick)));
                break;
        }

        var aparat = Aparat!;
        var sorov = new SotuvYaratishDto(aparat.Aparat.Id, SummaRejimi ? null : Litr, SummaRejimi ? Summa : null, tolovlar.ToArray(), _kalit);
        try
        {
            var s = await Malumot.SotuvYarat(sorov);
            Xabar = $"{Til.T("Saqlandi")}: {aparat.Nomi}, {Format.Litr(s.Litr)}, {Format.Som(s.Summa)}";
            XabarXato = false;
            Kiritilgan = ""; AralashNaqd = ""; AralashPlastik = ""; AralashClick = "";
            Tolov = 0;
            KalitniYangila();
        }
        catch (ApiXatosi e)
        {
            Xabar = e.Message;
            XabarXato = true;
        }
    }

    [RelayCommand]
    private void Tozalash()
    {
        Kiritilgan = ""; AralashNaqd = ""; AralashPlastik = ""; AralashClick = ""; Tolov = 0; Xabar = "";
    }

    private static long Raqam(string s)
    {
        var t = new string(s.Where(char.IsDigit).ToArray());
        return long.TryParse(t, out var v) ? v : 0;
    }

    private static decimal Kasr(string s)
    {
        s = s.Replace(',', '.');
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
