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
    public bool Eski => Q.EskiQarz;
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
    /// <summary>§8.11: dasturdan oldingi qarz to'lovi — berilgan nasiya emas (jamiga kirmaydi), muddati yo'q.</summary>
    public bool Eski => N.EskiQarz;
    public string Kim => $"#{N.SmenaId} · {QisqaIsm(N.OperatorIsmi)}";
    public string Summa => Format.Pul(N.Summa);
    public string Qaytgan => Format.Pul(N.Qaytgan);
    public bool QaytganBor => N.Qaytgan > 0;
    public string Qoldiq => Format.Pul(N.Qoldiq);
    public string Muddat => Eski ? "—" : Format.Sana(N.Muddat);
    public bool Faol => N.Qoldiq > 0;
    public bool Otgan => N.Holati == NasiyaHolati.MuddatiOtgan;
    public bool Yopilgan => N.Holati == NasiyaHolati.Yopilgan;
    public string Belgi => Eski ? Til.T("Nasiya_EskiQarzBelgi") : Yopilgan ? Til.F("Nasiyalar_YopilganSana", N.Yopildi is { } y ? Format.QisqaSana(y.ToLocalTime()) : "")
        : QaytishDialogVM.NasiyaKun(N.MuddatgachaKun);
    /// <summary>Muddat belgisi: o'tgan — qizil, 3 kun ichida — sariq, qolgan — ko'k, yopilgan — yashil.</summary>
    public string BelgiKlassi => Eski ? "kulrang" : Yopilgan ? "yashil" : Otgan ? "qizil" : N.MuddatgachaKun <= 3 ? "sariq" : "kok";
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

/// <summary>Operator filtri elementi (§8.12): Id null — "Hammasi".</summary>
public sealed record OperatorTanlovi(int? Id, string Ism)
{
    public override string ToString() => Ism;
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
    /// <summary>Ro'yxat manbai: operator tanlanmagan — hamma nasiyalar, tanlangan — GET /nasiyalar?operatorId= javobi.</summary>
    private NasiyalarDto? _malumot;
    /// <summary>Hamma nasiyalar — operator tanlovi ro'yxati shundan (yozuvlardagi MuallifId va OperatorIsmi).</summary>
    private NasiyalarDto? _hammasi;

    // ================= §8.12: operator filtri =================
    public List<OperatorTanlovi> Operatorlar { get; private set; } = new();
    [ObservableProperty] private OperatorTanlovi? _operator;
    public bool OperatorTanlangan => Operator?.Id is not null;
    public bool UmumiyKpi => !OperatorTanlangan;
    private NasiyaOperatorXulosaDto? OX => OperatorTanlangan ? _malumot?.Operator : null;
    public string OpBergan => Format.Pul(OX?.Berilgan ?? 0);
    public string OpBerganIzoh => Til.F("Nasiyalar_OpSoni", OX?.Soni ?? 0);
    public string OpQaytgan => Format.Pul(OX?.Qaytgan ?? 0);
    public string OpQoldiq => Format.Pul(OX?.Qoldiq ?? 0);
    /// <summary>FaolSoni — qoldig'i bor nasiyalar soni (mijozlar emas).</summary>
    public string OpQoldiqIzoh => Til.F("Nasiyalar_OpFaol", OX?.FaolSoni ?? 0);
    public string OpEskiQarz => Format.Pul(OX?.EskiQarzQabul ?? 0);
    public string OpIsm => OX?.OperatorIsmi ?? Operator?.Ism ?? "";

    private bool _operatorlarQurilmoqda;

    partial void OnOperatorChanged(OperatorTanlovi? value)
    {
        if (_operatorlarQurilmoqda) return;
        // Tanlov almashdi: eski operator ro'yxati/KPI'lari yangisi kelguncha ko'rinmasin
        _malumot = value?.Id is null ? _hammasi : null;
        Filtrla();
        if (value?.Id is not null) _yuklash.Rejala();
    }

    private void OperatorlarniQur()
    {
        var l = (_hammasi?.Royxat ?? []).GroupBy(n => n.MuallifId)
            .Select(g => new OperatorTanlovi(g.Key, g.OrderByDescending(n => n.Yozildi).First().OperatorIsmi))
            .OrderBy(o => o.Ism, StringComparer.CurrentCultureIgnoreCase).ToList();
        l.Insert(0, new OperatorTanlovi(null, Til.T("Nasiyalar_OperatorHammasi")));
        var id = Operator?.Id;
        _operatorlarQurilmoqda = true;
        Operatorlar = l;
        OnPropertyChanged(nameof(Operatorlar));
        Operator = l.FirstOrDefault(o => o.Id == id) ?? l[0];
        _operatorlarQurilmoqda = false;
    }
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
    public string FaolIzoh => Til.F("Nasiyalar_JamiQarzIzoh", X.FaolSoni, Format.Pul(X.MuddatiOtgan));
    public string OtganQarz => Format.Pul(X.MuddatiOtgan);
    public string OtganIzoh => Til.F("Nasiyalar_MijozSoni", X.MuddatiOtganSoni);
    public string OyBerilgan => Format.Pul(X.OyBerilgan);
    public string OyBerilganIzoh => Til.F("Nasiyalar_NasiyaSoni", X.OyBerilganSoni);
    public string OyQaytgan => Format.Pul(X.OyQaytgan);
    public string OyQaytganIzoh => Til.F("Nasiyalar_TolovSoni", X.OyQaytganSoni);
    public bool BoshQator => Qatorlar.Count == 0;
    // Jami qatori — joriy filtr va qidiruv bo'yicha ko'rinib turgan ro'yxatdan (§8.7).
    public bool JamiBor => Qatorlar.Count > 0;
    // §8.11: eski qarz yozuvlari berilgan nasiya emas — jamiga kirmaydi (jadvalda belgi bilan ko'rinadi)
    private IEnumerable<NasiyaDto> JamiManba => Qatorlar.Where(q => !q.Eski).Select(q => q.N);
    public string JamiQarz => Format.Pul(JamiManba.Sum(n => n.Summa));
    public string JamiQaytgan => Format.Pul(JamiManba.Sum(n => n.Qaytgan));
    public string JamiQoldiq => Format.Pul(JamiManba.Sum(n => n.Qoldiq));
    public bool EksportKorinsin => Joriy.Bor(Ruxsat.Eksport);

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
            else if (!Malumot.Kirilgan) { _malumot = _hammasi = null; Operator = null; OperatorlarniQur(); Filtrla(); }
            foreach (var n in new[] { nameof(QarzQaytdiOladi), nameof(NasiyaYozaOladi), nameof(SmenaOchiq), nameof(NasiyaYozishIzoh) }) OnPropertyChanged(n);
            NasiyaYozCommand.NotifyCanExecuteChanged();
            ExcelCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(EksportKorinsin));
        };
        Til.Ozgardi += () => { OperatorlarniQur(); Filtrla(); OnPropertyChanged(string.Empty); };
        OperatorlarniQur();
    }

    private async Task Yukla(Func<bool> dolzarb)
    {
        var hammasi = await Malumot.Api.Nasiyalar();
        if (!dolzarb() || !Malumot.Kirilgan) return;
        var opId = Operator?.Id;
        var d = opId is { } id ? await Malumot.Api.Nasiyalar(operatorId: id) : hammasi;
        if (!dolzarb() || !Malumot.Kirilgan || Operator?.Id != opId) return;
        _hammasi = hammasi;
        _malumot = d;
        OperatorlarniQur();
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
        ExcelCommand.NotifyCanExecuteChanged();
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
            // §8.11: eski qarz nasiyasi qaytishi bilan birga o'chadi (qaytishi bo'lsa ham)
            q.OchirishMumkin = (t.Qaytishlar.Length == 0 || q.Eski) && YozuvOchiriladimi(q.N.SmenaId, q.N.MuallifId);
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

    private bool EksportMumkin() => _malumot is not null && Joriy.Bor(Ruxsat.Eksport);

    /// <summary>§8.6: joriy filtr va qidiruv bo'yicha ko'rinib turgan ro'yxat → Documents\FuelControl\Nasiyalar\nasiyalar-YYYY-MM-DD.xlsx.</summary>
    [RelayCommand(CanExecute = nameof(EksportMumkin))]
    private async Task Excel()
    {
        string[] ustunlar = [Til.T("Nasiyalar_Mijoz"), Til.T("Nasiya_Telefon"), Til.T("Nasiya_MashinaRaqami"), Til.T("Nasiyalar_Yozilgan"),
            Til.T("Nasiyalar_ColSmena"), Til.T("Operator"), Til.T("Nasiya_Qarz"), Til.T("Nasiya_Qaytgan"), Til.T("Nasiya_Qoldiq"),
            Til.T("Nasiya_MuddatYorliq"), Til.T("Nasiyalar_ColHolat")];
        var qatorlar = Qatorlar.Select(q => (new object?[]
        {
            q.N.MijozIsmi, Format.Telefon(q.N.Telefon), q.N.MashinaRaqami, Format.Sana(q.N.Yozildi.ToLocalTime()), "#" + q.N.SmenaId,
            q.N.OperatorIsmi, q.N.Summa, q.N.Qaytgan, q.N.Qoldiq, q.Eski ? null : Format.Sana(q.N.Muddat),
            q.Belgi,   // eski qarz: faqat Nasiya_EskiQarzBelgi (har doim yopiq)
        }, false)).ToList();
        object?[] jami = [Til.T("Jami"), null, null, null, null, null,
            JamiManba.Sum(n => n.Summa), JamiManba.Sum(n => n.Qaytgan), JamiManba.Sum(n => n.Qoldiq), null, null];
        var filtr = Filtr switch { "faol" => Til.T("Nasiyalar_Faol"), "otgan" => Til.T("Nasiyalar_MuddatiOtgan"), "yopilgan" => Til.T("Nasiyalar_Yopilgan"), _ => Til.T("Nasiyalar_Hammasi") };
        var opMatn = OperatorTanlangan ? Til.F("Nasiyalar_ExcelOperator", OpIsm) : null;
        var izoh = $"{Format.Sana(DateTime.Today)} · {filtr}" + (opMatn is null ? "" : $" · {opMatn}") + (Qidiruv.Trim().Length > 0 ? $" · \"{Qidiruv.Trim()}\"" : "") + $" · {Qatorlar.Count}";
        try
        {
            var fayl = ExcelEksport.Saqla("Nasiyalar", $"nasiyalar-{DateTime.Today:yyyy-MM-dd}.xlsx", opMatn is null ? Til.T("Nasiyalar_ExcelSarlavha") : $"{Til.T("Nasiyalar_ExcelSarlavha")} · {opMatn}", izoh, ustunlar, qatorlar, jami);
            await Malumot.EksportniYoz("Nasiyalar", $"{izoh} — {System.IO.Path.GetFileName(fayl)}");
            Bildirish.Malumot($"{Til.T("FaylSaqlandi")}: {fayl}");
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { Bildirish.Xato(e.Message); }
    }

    private bool NasiyaYozMumkin() => SmenaOchiq && NasiyaYozaOladi;
    [RelayCommand(CanExecute = nameof(NasiyaYozMumkin))]
    private void NasiyaYoz() => Dialoglar.Nasiya.Och();

    [RelayCommand]
    private async Task NasiyaOchir(NasiyaQatori q)
    {
        if (!await Bildirish.Tasdiqla(Til.T("Nasiyalar_Ochirish"), Til.T(q.Eski ? "Nasiya_EskiQarzOchirish" : "Nasiyalar_OchirishSavol"), Til.T("Nasiyalar_HaOchirish"), xavfli: true)) return;
        try { await Malumot.NasiyaOchir(q.N.Id); }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }

    [RelayCommand]
    private async Task QaytishOchir(QaytishQatori q)
    {
        if (!await Bildirish.Tasdiqla(Til.T("Ochirish"), Til.T(q.Eski ? "Nasiya_EskiQarzOchirish" : "Nasiyalar_QaytishOchirishSavol"), Til.T("Nasiyalar_HaOchirish"), xavfli: true)) return;
        try { await Malumot.QaytishOchir(q.Q.Id); }
        catch (ApiXatosi e) { Bildirish.Xato(e.Message); }
    }
}
