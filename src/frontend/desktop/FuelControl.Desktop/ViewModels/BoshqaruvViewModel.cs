using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Grafik ustuni: oxirgi 14 yopilgan smena savdosi (million so'm).</summary>
public sealed record SavdoUstuni(string Sana, string Qiymat, double Balandlik, bool Oxirgi, string Izoh);

/// <summary>To'lov turi ulushi (shu oy).</summary>
public sealed record UlushQatori(string Nomi, string Summa, string Foiz, double Ulush, IBrush Rang);

/// <summary>Baklar qoldig'i qatori.</summary>
public sealed record BakQatori(int Raqam, YoqilgiBelgi Yoqilgi, string Kirim, string Qoldiq, bool Manfiy);

/// <summary>Oxirgi yopilgan smena qatori.</summary>
public sealed record YopilganQator(SmenaDto S)
{
    public string Raqam => "#" + S.Id;
    public string Vaqt => $"{Format.QisqaSana(S.Boshlandi.ToLocalTime())} · {Til.F("Boshqaruv_Soat", (int)Math.Round(((S.Tugadi ?? S.Boshlandi) - S.Boshlandi).TotalHours))}";
    public string Litr => Format.Son(S.JamiLitr);
    public string Savdo => Format.Pul(S.Savdo);
    public string FarqMatn => S.Farq switch
    {
        < 0 => $"{Til.T("Kamomat")} {Format.Farq(S.Farq)}",
        > 0 => $"{Til.T("Ortiqcha")} {Format.Farq(S.Farq)}",
        _ => Til.T("Boshqaruv_FarqYoq"),
    };
    public string FarqKlassi => S.Farq < 0 ? "qizil" : S.Farq > 0 ? "kok" : "yashil";
}

/// <summary>Boshqaruv paneli — /boshqaruv (faqat yopilgan smenalar + joriy smena holati), serverda hisoblanadi.</summary>
public partial class BoshqaruvViewModel : ObservableObject
{
    private readonly KechiktirilganIsh _yuklash;
    private BoshqaruvDto? _d;

    public BoshqaruvViewModel()
    {
        _yuklash = new KechiktirilganIsh(Yukla, 500);
        Malumot.Ozgardi += () =>
        {
            if (Malumot.Kirilgan && Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Boshqaruv)) _yuklash.Rejala();
            else if (!Malumot.Kirilgan && _d is not null) { _d = null; OnPropertyChanged(string.Empty); }
        };
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
    }

    private async Task Yukla(Func<bool> dolzarb)
    {
        var d = await Malumot.Api.Boshqaruv();
        if (!dolzarb() || !Malumot.Kirilgan) return;
        _d = d;
        OnPropertyChanged(string.Empty);
    }

    public Task HozirYukla() => _yuklash.Bajar();

    [RelayCommand]
    private async Task Yangilash()
    {
        try { await Malumot.QaytaYukla(); }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); return; }
        await _yuklash.Bajar();
    }

    [RelayCommand] private void OylikHisobot() => Navigatsiya.Och(MainViewModel.SHisobot);
    [RelayCommand] private void BarchaSmenalar() => Navigatsiya.Och(MainViewModel.SSmenalar);
    [RelayCommand] private void SavdogaOt() => Navigatsiya.Och(MainViewModel.SSavdo);
    [RelayCommand] private void BakKirim() => Dialoglar.Bak.Och(null);
    public bool BakKirimOladi => Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.BakKirim);

    /// <summary>"Yakshanba, 4-oktabr 2026" — joriy til lug'atidan.</summary>
    public string Sana
    {
        get
        {
            var b = DateTime.Today;
            var kunlar = Til.T("Boshqaruv_HaftaKunlari").Split(',');
            var oylar = Til.T("Boshqaruv_OyNomlari").Split(',');
            return Til.F("Boshqaruv_SanaShakli", kunlar[(int)b.DayOfWeek], b.Day, oylar[b.Month - 1], b.Year);
        }
    }

    // ---- KPI
    private SmenaDto? Oxirgi => _d?.OxirgiYopilgan;
    public string OxirgiSavdo => Format.Pul(Oxirgi?.Savdo ?? 0);
    public string OxirgiIzoh => Oxirgi is { } o ? Til.F("Boshqaruv_OxirgiSmenaIzoh", o.Id, o.OperatorIsmi, Format.Son(o.JamiLitr)) : Til.T("Boshqaruv_YopilganYoq");
    public string OySavdo => Format.Pul(_d?.OySavdo ?? 0);
    public string OyIzoh => Til.F("Boshqaruv_OySmenaIzoh", _d?.OySmenaSoni ?? 0, Format.Son(_d?.OyLitr ?? 0));
    public string OyKamomat => Format.Pul(_d?.OyKamomat ?? 0);
    public string OrtiqchaIzoh => (_d?.OyOrtiqcha ?? 0) > 0
        ? Til.F("Boshqaruv_OrtiqchaIzoh", Format.Pul(_d!.OyOrtiqcha), _d.OxirgiSmenalar.Count(s => s.Farq > 0))
        : Til.T("Boshqaruv_OrtiqchaYoq");
    public string OtganNasiya => Format.Pul(_d?.Nasiyalar.MuddatiOtgan ?? 0);
    public string OtganIzoh => Til.F("Boshqaruv_QarzIzoh", _d?.Nasiyalar.MuddatiOtganSoni ?? 0, Format.Pul(_d?.Nasiyalar.FaolQarz ?? 0));

    // ---- Joriy smena
    private SmenaDto? J => _d?.JoriySmena;
    public bool JoriyBor => J is not null;
    public bool JoriyYoq => J is null;
    public string JoriySarlavha => J is { } j ? Til.F("Boshqaruv_JoriySmena", j.Id, j.OperatorIsmi) : "";
    public string JoriyHarflar => Format.BoshHarflar(J?.OperatorIsmi ?? "");
    public string JoriyIzoh => J is { } j
        ? Til.F("Boshqaruv_BoshlanganDavomiylik", Format.QisqaSanaVaqt(j.Boshlandi.ToLocalTime()), Format.Davomiylik(DateTime.UtcNow - j.Boshlandi))
        : "";
    public string JQaytim => Format.Pul(J?.OchishQaytim ?? 0);
    public string JTerminal => Format.Pul(J?.OchishTerminal ?? 0);
    public string JDepozit => Format.Pul(J?.OchishDepozit ?? 0);
    public string JNasiya => Format.Pul(J?.NasiyaJami ?? 0);
    public string JQaytgan => Format.Pul(J?.QaytganNasiya ?? 0);
    public string JXarajat => Format.Pul(J?.XarajatJami ?? 0);
    // Soni Malumot.Joriy dan (BoshqaruvDto da soni yo'q).
    public string JNasiyaYorliq => Til.F("Boshqaruv_NasiyaSoni", Malumot.Joriy?.Nasiyalar.Length ?? 0).ToUpperInvariant();
    public string JXarajatYorliq => Til.F("Boshqaruv_XarajatSoni", Malumot.Joriy?.Xarajatlar.Length ?? 0).ToUpperInvariant();
    public string OxirgiSmenaSarlavha => Oxirgi is { } o ? Til.F("Boshqaruv_OxirgiSmena", o.Id) : Til.T("Boshqaruv_YopilganYoq");
    public string OxirgiYopildi => Oxirgi?.Tugadi is { } t ? $"{Til.T("Boshqaruv_Yopildi")}: {Format.SanaVaqt(t.ToLocalTime())} · {Oxirgi.OperatorIsmi}" : "";
    public bool SavdogaOtaOladi => Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Savdo);

    // ---- Baklar
    public List<BakQatori> Baklar => (_d?.Aparatlar ?? []).OrderBy(a => a.Raqam).Select(a =>
    {
        var y = Malumot.Yoqilgilar.FirstOrDefault(x => x.Id == a.YoqilgiTuriId);
        return new BakQatori(a.Raqam, YoqilgiBelgi.Yarat(a.YoqilgiNomi, y?.Rang ?? "#2F6BFF"),
            a.OxirgiKirimVaqti is { } v ? Til.F("Boshqaruv_KirimSana", Format.QisqaSana(v.ToLocalTime())) : "",
            Format.ButunLitr(a.BakQoldiq) + " L", a.BakQoldiq < 0);
    }).ToList();
    public string BakHolati => Til.F("Boshqaruv_HolatigaVaqt", Oxirgi?.Tugadi is { } t ? Format.QisqaSanaVaqt(t.ToLocalTime()) : Format.QisqaSanaVaqt(DateTime.Now));

    // ---- Grafik
    public List<SavdoUstuni> Ustunlar
    {
        get
        {
            var l = _d?.OxirgiSmenalar ?? [];
            if (l.Length == 0) return new();
            var maks = Math.Max(1, l.Max(x => x.Savdo));
            return l.Select((x, i) => new SavdoUstuni(Format.QisqaSana(x.Sana),
                (x.Savdo / 1_000_000m).ToString("0.0", CultureInfo.InvariantCulture),
                Math.Max(8, 150.0 * x.Savdo / maks), i == l.Length - 1,
                $"#{x.Id} · {x.OperatorIsmi} · {Format.Som(x.Savdo)}")).ToList();
        }
    }

    // ---- To'lov turlari
    public List<UlushQatori> Tolovlar
    {
        get
        {
            var t = _d?.OyTolovlar ?? new TolovTaqsimotiDto(0, 0, 0, 0);
            var jami = Math.Max(1, t.Naqd + t.Plastik + t.Depozit + t.Nasiya);
            UlushQatori Q(string kalit, long s, string rang) =>
                new(Til.T(kalit), Format.Pul(s), $"{Math.Round(100.0 * s / jami)}%", (double)s / jami, new SolidColorBrush(Color.Parse(rang)));
            return new[]
            {
                Q("Tolov_Plastik", t.Plastik, "#19B5C9"), Q("Tolov_Naqd", t.Naqd, "#2F6BFF"),
                Q("Tolov_Depozit", t.Depozit, "#7C5CFF"), Q("Tolov_Nasiya", t.Nasiya, "#E8590C"),
            }.OrderByDescending(x => x.Ulush).ToList();
        }
    }

    // ---- Oxirgi yopilganlar
    public List<YopilganQator> OxirgiYopilganlar => (_d?.OxirgiYopilganlar ?? []).Select(s => new YopilganQator(s)).ToList();
    public bool SmenalarniKoradi => Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Smenalar);
}
