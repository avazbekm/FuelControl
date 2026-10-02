using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

public sealed record YoqilgiQatori(string Nomi, string Rang, string Litr, string Summa, double Ulush);
public sealed record TolovQatori(string Nomi, string Summa, string Foiz, double Ulush);
public sealed record KunUstuni(string Kun, string Summa, double Balandlik, bool Bugun);
public sealed record OperatorKunQatori(Foydalanuvchi Operator, string Summa, string Litr, int SotuvSoni, bool Ochiq);

/// <summary>Boshqaruv paneli — bugungi holat bir qarashda. Ko'rsatkichlar serverda hisoblanadi (/boshqaruv/bugun).</summary>
public partial class BoshqaruvViewModel : ObservableObject
{
    public string Sana => DateTime.Today.ToString("d MMMM, dddd");

    public string JamiSavdo { get; private set; } = "";
    public string JamiLitr { get; private set; } = "";
    public string SotuvSoni { get; private set; } = "";
    public string Naqd { get; private set; } = "";
    public string Plastik { get; private set; } = "";
    public string Click { get; private set; } = "";
    public string OchiqSmenalar { get; private set; } = "";
    public string OyJami { get; private set; } = "";
    public string OyKamomat { get; private set; } = "";
    public string KechagigaNisbatan { get; private set; } = "";
    public bool KechagidanKop { get; private set; }

    public List<YoqilgiQatori> Yoqilgilar { get; private set; } = new();
    public List<TolovQatori> Tolovlar { get; private set; } = new();
    public List<KunUstuni> OxirgiKunlar { get; private set; } = new();
    public List<OperatorKunQatori> BugungiOperatorlar { get; private set; } = new();
    public List<Sotuv> OxirgiSotuvlar { get; private set; } = new();

    private readonly KechiktirilganIsh _yuklash;
    private BoshqaruvBugunDto? _oxirgi;

    public BoshqaruvViewModel()
    {
        _yuklash = new KechiktirilganIsh(Yukla, 500);
        Korsat(null);
        Til.Ozgardi += () => { Korsat(_oxirgi); OnPropertyChanged(string.Empty); };
        // Kesh har o'zgarganda (SignalR xabari, o'z amalimiz) — panel serverdan qayta so'raladi.
        Malumot.Ozgardi += () =>
        {
            if (Malumot.Kirilgan && Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Boshqaruv)) _yuklash.Rejala();
            else if (!Malumot.Kirilgan && _oxirgi is not null) { _oxirgi = null; Korsat(null); OnPropertyChanged(string.Empty); }
        };
    }

    private async Task Yukla(Func<bool> dolzarb)
    {
        var d = await Malumot.Api.Boshqaruv();
        if (!dolzarb() || !Malumot.Kirilgan) return;
        _oxirgi = d;
        Korsat(d);
        OnPropertyChanged(string.Empty);
    }

    /// <summary>"Yangilash" — butun kesh va panelni serverdan qayta oladi.</summary>
    [RelayCommand]
    private async Task Yangilash()
    {
        try { await Malumot.QaytaYukla(); }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); return; }
        await _yuklash.Bajar();
    }

    private void Korsat(BoshqaruvBugunDto? d)
    {
        var k = d?.Kpi;
        long jami = k?.BugungiSumma ?? 0, kecha = k?.KechagiSumma ?? 0;
        JamiSavdo = Format.Pul(jami);
        JamiLitr = Format.Litr(k?.BugungiLitr ?? 0);
        SotuvSoni = (k?.SotuvSoni ?? 0).ToString();
        OchiqSmenalar = (k?.OchiqSmenalar ?? 0).ToString();
        OyJami = Format.Pul(k?.OyJami ?? 0);
        OyKamomat = Format.Pul(k?.OyKamomat ?? 0);

        long Tolov(TolovTuri t) => d?.TolovUlushlari.FirstOrDefault(x => x.Turi == t)?.Summa ?? 0;
        long naqd = Tolov(TolovTuri.Naqd), plastik = Tolov(TolovTuri.Plastik), click = Tolov(TolovTuri.Click);
        Naqd = Format.Pul(naqd);
        Plastik = Format.Pul(plastik);
        Click = Format.Pul(click);

        KechagidanKop = jami >= kecha;
        var foiz = kecha == 0 ? 0 : (double)(jami - kecha) / kecha * 100;
        KechagigaNisbatan = $"{(foiz >= 0 ? "▲" : "▼")} {Math.Abs(foiz):0}% {Til.T("KechagigaNisbatan")}";

        Yoqilgilar = (d?.YoqilgiUlushlari ?? []).Select(y =>
            new YoqilgiQatori(y.Nomi, y.Rang, Format.Litr(y.Litr), Format.Pul(y.Summa), jami == 0 ? 0 : (double)y.Summa / jami)).ToList();

        Tolovlar =
        [
            new(Til.T("Naqd"), Format.Pul(naqd), Foiz(naqd, jami), Ulush(naqd, jami)),
            new(Til.T("Plastik"), Format.Pul(plastik), Foiz(plastik, jami), Ulush(plastik, jami)),
            new(Til.T("Click"), Format.Pul(click), Foiz(click, jami), Ulush(click, jami)),
        ];

        var kunlar = d?.OxirgiKunlar ?? Enumerable.Range(0, 14).Select(i => new KunlikDto(Malumot.Bugun.AddDays(-13 + i), 0, 0)).ToArray();
        long maks = Math.Max(1, kunlar.Max(x => x.Summa));
        OxirgiKunlar = kunlar.Select(x => new KunUstuni(x.Sana.ToString("dd"), Format.Pul(x.Summa),
            Math.Max(4, 120.0 * x.Summa / maks), x.Sana == Malumot.Bugun)).ToList();

        BugungiOperatorlar = (d?.Operatorlar ?? []).Select(o => new OperatorKunQatori(
            Malumot.FoydalanuvchiniOl(o.OperatorId, o.Ism), Format.Pul(o.BugungiSumma), Format.Litr(o.BugungiLitr), o.SotuvSoni, o.SmenaOchiqmi)).ToList();

        OxirgiSotuvlar = (d?.OxirgiSotuvlar ?? []).Select(Malumot.SotuvKorinishi).ToList();
    }

    private static string Foiz(long qism, long jami) => jami == 0 ? "0%" : $"{100.0 * qism / jami:0}%";
    private static double Ulush(long qism, long jami) => jami == 0 ? 0 : (double)qism / jami;
}
