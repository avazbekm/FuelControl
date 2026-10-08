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

/// <summary>Qaytish qatori (nasiya tafsilotida).</summary>
public sealed record QaytishQatori(NasiyaQaytishiDto Q, bool OchirishMumkin)
{
    public string Vaqt => Format.QisqaSanaVaqt(Q.Vaqt.ToLocalTime());
    public string Summa => "+" + Format.Pul(Q.Summa);
    public string Izoh => $"{Til.T("Tolov_" + Q.Usul)} · {Q.KimYozdi} · " +
        (Q.SmenaId is { } s ? Til.F("Smenalar_Smena", s) : Til.T("Nasiyalar_SmenadanTashqari")) +
        (string.IsNullOrWhiteSpace(Q.Izoh) ? "" : $" · \"{Q.Izoh}\"");
}

/// <summary>Nasiyalar jadvalidagi qator; bosilsa qaytishlar tarixi ochiladi.</summary>
public partial class NasiyaQatori : ObservableObject
{
    public NasiyaDto N { get; }
    public NasiyaQatori(NasiyaDto n) => N = n;

    public string Telefon => Format.Telefon(N.Telefon);
    public string Hudud => DavlatRaqam.Hudud(N.MashinaRaqami);
    public string Raqam => DavlatRaqam.Asosiy(N.MashinaRaqami);
    public bool RaqamBor => !string.IsNullOrWhiteSpace(N.MashinaRaqami);
    public bool HududBor => Hudud.Length > 0;
    public string Yozilgan => Format.QisqaSana(N.Yozildi.ToLocalTime());
    public string Kim => $"#{N.SmenaId} · {QisqaIsm(N.OperatorIsmi)}";
    public string Summa => Format.Pul(N.Summa);
    public string Qaytgan => Format.Pul(N.Qaytgan);
    public bool QaytganBor => N.Qaytgan > 0;
    public string Qoldiq => Format.Pul(N.Qoldiq);
    public string Muddat => Format.Sana(N.Muddat);
    public bool Faol => N.Qoldiq > 0;
    public bool Otgan => N.Holati == NasiyaHolati.MuddatiOtgan;
    public bool Yopilgan => N.Holati == NasiyaHolati.Yopilgan;
    public string Belgi => Yopilgan ? Til.F("Nasiyalar_YopilganSana", N.Yopildi is { } y ? Format.QisqaSana(y.ToLocalTime()) : "")
        : QaytishDialogVM.NasiyaKun(N.MuddatgachaKun);
    /// <summary>Muddat belgisi: o'tgan — qizil, 3 kun ichida — sariq, qolgan — ko'k, yopilgan — yashil.</summary>
    public string BelgiKlassi => Yopilgan ? "yashil" : Otgan ? "qizil" : N.MuddatgachaKun <= 3 ? "sariq" : "kok";
    public string QaytdiIzoh => Til.F("Nasiyalar_QaytdiAria", N.MijozIsmi);

    [ObservableProperty] private bool _ochiq;
    [ObservableProperty] private List<QaytishQatori> _qaytishlar = new();
    [ObservableProperty] private bool _yuklandi;
    [ObservableProperty] private bool _ochirishMumkin;
    public bool QaytishYoq => Yuklandi && Qaytishlar.Count == 0;
    partial void OnQaytishlarChanged(List<QaytishQatori> value) => OnPropertyChanged(nameof(QaytishYoq));
    partial void OnYuklandiChanged(bool value) => OnPropertyChanged(nameof(QaytishYoq));

    /// <summary>"Alisher Karimov" → "Alisher K."</summary>
    public static string QisqaIsm(string ism)
    {
        var q = ism.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return q.Length >= 2 ? $"{q[0]} {q[1][0]}." : ism;
    }
}

/// <summary>Davlat raqami: "40 C 919 DA" → hudud "40" va asosiy qism "C 919 DA".</summary>
public static class DavlatRaqam
{
    public static string Hudud(string raqam)
    {
        var t = (raqam ?? "").Trim();
        return t.Length > 3 && char.IsDigit(t[0]) && char.IsDigit(t[1]) && t[2] == ' ' ? t[..2] : "";
    }

    public static string Asosiy(string raqam)
    {
        var t = (raqam ?? "").Trim();
        return Hudud(t).Length > 0 ? t[3..].Trim() : t;
    }
}

/// <summary>
/// Nasiyalar: xulosa kartalari, holat filtri (hammasi / faol / muddati o'tgan / yopilgan) va qidiruv (ism, telefon, raqam).
/// Ro'yxat serverdan bir marta to'liq olinadi, filtr va qidiruv klientda (sonlar chiplarda ko'rinadi). Qator bosilsa — qaytishlar tarixi.
/// </summary>
public partial class NasiyalarViewModel : ObservableObject
{
    public ObservableCollection<NasiyaQatori> Qatorlar { get; } = new();
    private NasiyalarDto? _malumot;
    private readonly KechiktirilganIsh _yuklash;

    [ObservableProperty] private string _qidiruv = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FHammasi), nameof(FFaol), nameof(FOtgan), nameof(FYopilgan))]
    private string _filtr = "";

    public bool FHammasi => Filtr == "";
    public bool FFaol => Filtr == "faol";
    public bool FOtgan => Filtr == "otgan";
    public bool FYopilgan => Filtr == "yopilgan";

    private IEnumerable<NasiyaDto> Hammasi => _malumot?.Royxat ?? [];
    public int SoniHammasi => Hammasi.Count();
    public int SoniFaol => Hammasi.Count(n => n.Qoldiq > 0);
    public int SoniOtgan => Hammasi.Count(n => n.Holati == NasiyaHolati.MuddatiOtgan);
    public int SoniYopilgan => Hammasi.Count(n => n.Holati == NasiyaHolati.Yopilgan);

    private NasiyalarXulosaDto X => _malumot?.Xulosa ?? new NasiyalarXulosaDto(0, 0, 0, 0, 0, 0, 0, 0);
    public string FaolQarz => Format.Pul(X.FaolQarz);
    public string FaolIzoh => Til.F("Nasiyalar_MijozSoni", X.FaolSoni);
    public string OtganQarz => Format.Pul(X.MuddatiOtgan);
    public string OtganIzoh => Til.F("Nasiyalar_MijozSoni", X.MuddatiOtganSoni);
    public string OyBerilgan => Format.Pul(X.OyBerilgan);
    public string OyBerilganIzoh => Til.F("Nasiyalar_NasiyaSoni", X.OyBerilganSoni);
    public string OyQaytgan => Format.Pul(X.OyQaytgan);
    public string OyQaytganIzoh => Til.F("Nasiyalar_TolovSoni", X.OyQaytganSoni);
    public bool BoshQator => Qatorlar.Count == 0;

    private static Foydalanuvchi Joriy => Malumot.JoriyFoydalanuvchi;
    public bool QarzQaytdiOladi => Joriy.Bor(Ruxsat.QarzQaytdi);
    public bool NasiyaYozaOladi => Joriy.Bor(Ruxsat.NasiyaYozish);
    public bool SmenaOchiq => Malumot.JoriySmena is not null;
    public string NasiyaYozishIzoh => SmenaOchiq ? Til.T("Nasiya_Yozish") : Til.T("Nasiyalar_SmenaKerak");

    public NasiyalarViewModel()
    {
        _yuklash = new KechiktirilganIsh(Yukla, 300);
        Malumot.Ozgardi += () =>
        {
            if (Malumot.Kirilgan && Joriy.Bor(Ruxsat.Nasiyalar)) _yuklash.Rejala();
            else if (!Malumot.Kirilgan) { _malumot = null; Filtrla(); }
            foreach (var n in new[] { nameof(QarzQaytdiOladi), nameof(NasiyaYozaOladi), nameof(SmenaOchiq), nameof(NasiyaYozishIzoh) }) OnPropertyChanged(n);
            NasiyaYozCommand.NotifyCanExecuteChanged();
        };
        Til.Ozgardi += () => { Filtrla(); OnPropertyChanged(string.Empty); };
    }

    private async Task Yukla(Func<bool> dolzarb)
    {
        var d = await Malumot.Api.Nasiyalar();
        if (!dolzarb() || !Malumot.Kirilgan) return;
        _malumot = d;
        Filtrla();
    }

    /// <summary>Hozir yuklash (harness va "Yangilash" uchun).</summary>
    public Task HozirYukla() => _yuklash.Bajar();

    partial void OnQidiruvChanged(string value) => Filtrla();
    partial void OnFiltrChanged(string value) => Filtrla();

    [RelayCommand] private void FiltrTanla(string f) => Filtr = f;

    private void Filtrla()
    {
        var ochiqId = Qatorlar.FirstOrDefault(q => q.Ochiq)?.N.Id;
        Qatorlar.Clear();
        var q = Qidiruv.Trim();
        foreach (var n in Hammasi.Where(n => Filtr switch
                 {
                     "faol" => n.Qoldiq > 0,
                     "otgan" => n.Holati == NasiyaHolati.MuddatiOtgan,
                     "yopilgan" => n.Holati == NasiyaHolati.Yopilgan,
                     _ => true,
                 })
                 .Where(n => q.Length == 0 || NasiyaQidiruv.Moslik(n, q) > 0)
                 // Muddati o'tganlar — eng ko'p o'tganidan; keyin muddat bo'yicha; yopilganlar oxirida.
                 .OrderBy(n => n.Qoldiq > 0 ? 0 : 1).ThenBy(n => n.Muddat).ThenByDescending(n => n.Id))
            Qatorlar.Add(new NasiyaQatori(n));
        OnPropertyChanged(string.Empty);
        if (ochiqId is { } id && Qatorlar.FirstOrDefault(x => x.N.Id == id) is { } qayta) _ = TafsilotOch(qayta);
    }

    [RelayCommand]
    private async Task Tafsilot(NasiyaQatori q)
    {
        if (q.Ochiq) { q.Ochiq = false; return; }
        foreach (var x in Qatorlar) x.Ochiq = false;
        await TafsilotOch(q);
    }

    private async Task TafsilotOch(NasiyaQatori q)
    {
        q.Ochiq = true;
        try
        {
            var t = (await Malumot.Api.NasiyaTafsiloti(q.N.Id))!;
            q.Qaytishlar = t.Qaytishlar.OrderByDescending(x => x.Vaqt).Select(x => new QaytishQatori(x, QaytishOchiriladimi(x))).ToList();
            q.OchirishMumkin = t.Qaytishlar.Length == 0 && YozuvOchiriladimi(q.N.SmenaId, q.N.MuallifId);
            q.Yuklandi = true;
        }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    /// <summary>§7.4–7.5: faqat ochiq smenadagi yozuv, muallif yoki boshliq.</summary>
    private static bool YozuvOchiriladimi(int? smenaId, int muallifId) =>
        smenaId is { } s && Malumot.JoriySmena?.Id == s && (Joriy.Boshliqmi || muallifId == Joriy.Id);

    /// <summary>Smenaga bog'lanmagan qaytishni faqat boshliq o'chiradi.</summary>
    private static bool QaytishOchiriladimi(NasiyaQaytishiDto q) =>
        q.SmenaId is null ? Joriy.Boshliqmi : YozuvOchiriladimi(q.SmenaId, q.MuallifId);

    [RelayCommand]
    private void Qaytdi(NasiyaQatori? q) => Dialoglar.Qaytish.Och(q?.N);

    private bool NasiyaYozMumkin() => SmenaOchiq && NasiyaYozaOladi;
    [RelayCommand(CanExecute = nameof(NasiyaYozMumkin))]
    private void NasiyaYoz() => Dialoglar.Nasiya.Och();

    [RelayCommand]
    private async Task NasiyaOchir(NasiyaQatori q)
    {
        if (!await Bildirish.Tasdiqla(Til.T("Nasiyalar_Ochirish"), Til.T("Nasiyalar_OchirishSavol"), Til.T("Nasiyalar_HaOchirish"), xavfli: true)) return;
        try { await Malumot.NasiyaOchir(q.N.Id); }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    [RelayCommand]
    private async Task QaytishOchir(QaytishQatori q)
    {
        if (!await Bildirish.Tasdiqla(Til.T("Ochirish"), Til.T("Nasiyalar_QaytishOchirishSavol"), Til.T("Nasiyalar_HaOchirish"), xavfli: true)) return;
        try { await Malumot.QaytishOchir(q.Q.Id); }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }
}
