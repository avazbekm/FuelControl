using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FuelControl.Contracts.Dto;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>ComboBox uchun aparat elementi.</summary>
public sealed class AparatTanlovi
{
    public Aparat Aparat { get; init; } = null!;
    public string Nomi => $"{Til.T("Aparat")} {Aparat.Raqam} · {Aparat.Yoqilgi.Nomi}";
}

/// <summary>
/// Kiritilgan sotuvni tahrirlash / bekor qilish (ruxsat bilan). Sotuv o'chirilmaydi:
/// o'zgarish sabab bilan audit jurnaliga yoziladi, totalizator va smena yakunlari qayta hisoblanadi.
/// </summary>
public partial class SotuvTahrirViewModel : ObservableObject
{
    [ObservableProperty] private bool _ochiq;
    [ObservableProperty] private Sotuv? _sotuv;
    [ObservableProperty] private List<AparatTanlovi> _aparatlar = new();
    [ObservableProperty] private AparatTanlovi? _aparat;
    [ObservableProperty] private string _summa = "";
    [ObservableProperty] private int _tolov; // 0 naqd, 1 plastik, 2 click, 3 aralash
    [ObservableProperty] private string _aralashNaqd = "";
    [ObservableProperty] private string _aralashPlastik = "";
    [ObservableProperty] private string _aralashClick = "";
    [ObservableProperty] private string _sabab = "";
    [ObservableProperty] private string _xato = "";

    private static Foydalanuvchi Joriy => Malumot.JoriyFoydalanuvchi;

    public string Sarlavha => Sotuv is null ? "" : Til.F("SotuvniTahrirlashSarlavha", Sotuv.Id);
    public string Izoh => Sotuv is null ? "" : $"{Sotuv.Operator.ToliqIsm} · {Format.SanaVaqt(Sotuv.Vaqt)} · {Til.T("Smena")} #{Sotuv.SmenaId}";
    public string Eski => Sotuv is null ? "" : Tavsif(Sotuv.Aparat, Sotuv.Litr, Sotuv.Summa, Sotuv.TolovNomi);
    public bool BekorQilishMumkin => Sotuv is { Faolmi: true } && Joriy.Bor(Ruxsat.SotuvBekorQilish);

    public bool TolovNaqd => Tolov == 0;
    public bool TolovPlastik => Tolov == 1;
    public bool TolovClick => Tolov == 2;
    public bool TolovAralash => Tolov == 3;

    // Server qoidasi: yoqilg'i turi o'zgarmasa sotuvdagi narx qoladi, aks holda yangi yoqilg'ining joriy narxi.
    private long Narx => Aparat is null ? 0 : (Sotuv is not null && Aparat.Aparat.Yoqilgi.Id == Sotuv.Aparat.Yoqilgi.Id ? Sotuv.Narx : Aparat.Aparat.Yoqilgi.Narx);
    private long SummaQiymat => Raqam(Summa);
    private decimal Litr => Narx == 0 ? 0 : Math.Round((decimal)SummaQiymat / Narx, 2, MidpointRounding.AwayFromZero);
    public string HisobIzohi => $"{Format.Pul(SummaQiymat)} ÷ {Format.Pul(Narx)} = {Format.Litr(Litr)}";
    public string AralashQoldiq => Format.Farq(SummaQiymat - (Raqam(AralashNaqd) + Raqam(AralashPlastik) + Raqam(AralashClick)));

    public SotuvTahrirViewModel()
    {
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
        Malumot.AloqaOzgardi += () => { SaqlaCommand.NotifyCanExecuteChanged(); BekorQilCommand.NotifyCanExecuteChanged(); };
    }

    private static bool AloqaBor() => Malumot.AloqaBor;

    partial void OnSummaChanged(string value) => Yangila();
    partial void OnAparatChanged(AparatTanlovi? value) => Yangila();
    partial void OnAralashNaqdChanged(string value) => Yangila();
    partial void OnAralashPlastikChanged(string value) => Yangila();
    partial void OnAralashClickChanged(string value) => Yangila();
    partial void OnTolovChanged(int value)
    {
        foreach (var n in new[] { nameof(TolovNaqd), nameof(TolovPlastik), nameof(TolovClick), nameof(TolovAralash) }) OnPropertyChanged(n);
    }

    private void Yangila()
    {
        OnPropertyChanged(nameof(HisobIzohi));
        OnPropertyChanged(nameof(AralashQoldiq));
    }

    [RelayCommand]
    public void Och(Sotuv s)
    {
        if (!Joriy.Bor(Ruxsat.SotuvTahrirlash)) return;
        Sotuv = s;
        Aparatlar = Malumot.Aparatlar.OrderBy(a => a.Raqam).Select(a => new AparatTanlovi { Aparat = a }).ToList();
        Aparat = Aparatlar.FirstOrDefault(a => a.Aparat == s.Aparat);
        Summa = Format.Pul(s.Summa);
        Tolov = s.Tolovlar.Count > 1 ? 3 : s.Tolovlar.Count == 0 ? 0 : (int)s.Tolovlar[0].Turi;
        AralashNaqd = s.Naqd > 0 ? Format.Pul(s.Naqd) : "";
        AralashPlastik = s.Plastik > 0 ? Format.Pul(s.Plastik) : "";
        AralashClick = s.Click > 0 ? Format.Pul(s.Click) : "";
        Sabab = ""; Xato = "";
        OnPropertyChanged(string.Empty);
        Ochiq = true;
    }

    [RelayCommand] private void Yop() => Ochiq = false;
    [RelayCommand] private void TolovniTanla(string t) => Tolov = int.Parse(t);

    /// <summary>Server tahrirlaydi: totalizator va smena yakunlarini qayta hisoblaydi, auditga yozadi.</summary>
    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task Saqla()
    {
        if (Sotuv is null || Aparat is null) return;
        if (Sabab.Trim().Length == 0) { Xato = Til.T("SababMajbur"); return; }
        if (SummaQiymat <= 0) { Xato = Til.T("Xato_Maydon"); return; }

        var tolovlar = new List<TolovDto>();
        switch (Tolov)
        {
            case 0: tolovlar.Add(new(TolovTuri.Naqd, SummaQiymat)); break;
            case 1: tolovlar.Add(new(TolovTuri.Plastik, SummaQiymat)); break;
            case 2: tolovlar.Add(new(TolovTuri.Click, SummaQiymat)); break;
            default:
                if (Raqam(AralashNaqd) + Raqam(AralashPlastik) + Raqam(AralashClick) != SummaQiymat) { Xato = Til.T("AralashIzoh"); return; }
                if (Raqam(AralashNaqd) > 0) tolovlar.Add(new(TolovTuri.Naqd, Raqam(AralashNaqd)));
                if (Raqam(AralashPlastik) > 0) tolovlar.Add(new(TolovTuri.Plastik, Raqam(AralashPlastik)));
                if (Raqam(AralashClick) > 0) tolovlar.Add(new(TolovTuri.Click, Raqam(AralashClick)));
                break;
        }

        Xato = "";
        try
        {
            await Malumot.SotuvTahrirla(Sotuv.Id, new SotuvTahrirlashDto(Aparat.Aparat.Id, SummaQiymat, tolovlar.ToArray(), Sabab.Trim()));
            Ochiq = false;
        }
        catch (ApiXatosi e)
        {
            Xato = e.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task BekorQil()
    {
        if (Sotuv is null || !BekorQilishMumkin) return;
        if (Sabab.Trim().Length == 0) { Xato = Til.T("SababMajbur"); return; }
        Xato = "";
        try
        {
            await Malumot.SotuvBekorQil(Sotuv.Id, Sabab.Trim());
            Ochiq = false;
        }
        catch (ApiXatosi e)
        {
            Xato = e.Message;
        }
    }

    private static string Tavsif(Aparat a, decimal litr, long summa, string tolov) =>
        $"{Til.T("Aparat")} {a.Raqam} {a.Yoqilgi.Nomi}, {Format.Litr(litr)}, {Format.Som(summa)}, {tolov}";

    private static long Raqam(string s)
    {
        var t = new string(s.Where(char.IsDigit).ToArray());
        return long.TryParse(t, out var v) ? v : 0;
    }
}
