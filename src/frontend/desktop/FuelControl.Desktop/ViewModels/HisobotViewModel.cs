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

public sealed record HisobotQatori(string Guruh, string Yoqilgi, string Litr, string Summa, string Naqd, string Plastik, string Click, int Soni, bool Jami);

/// <summary>Hisobotlar: sana oralig'i × operator × yoqilg'i × to'lov turi. Kunlik / oylik rejim. Hisob serverda (/hisobot).</summary>
public partial class HisobotViewModel : ObservableObject
{
    public List<Foydalanuvchi> OperatorFiltri { get; private set; } = new();
    public ObservableCollection<HisobotQatori> Qatorlar { get; } = new();

    [ObservableProperty] private Foydalanuvchi? _tanlanganOperator;
    [ObservableProperty] private DateTimeOffset _dan = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTimeOffset _gacha = DateTime.Today;
    [ObservableProperty] private int _guruhlash; // 0 operator, 1 kun, 2 oy

    public bool OperatorBoyicha => Guruhlash == 0;
    public bool KunBoyicha => Guruhlash == 1;
    public bool OyBoyicha => Guruhlash == 2;
    public string GuruhSarlavha => Guruhlash switch { 0 => Til.T("Operator"), 1 => Til.T("Sana"), _ => Til.T("Oy") };

    public string JamiSumma { get; private set; } = "";
    public string JamiLitr { get; private set; } = "";
    public string JamiNaqd { get; private set; } = "";
    public string JamiPlastik { get; private set; } = "";
    public string JamiClick { get; private set; } = "";
    public string JamiKamomat { get; private set; } = "";
    public string JamiAvans { get; private set; } = "";
    public string BekorSoni { get; private set; } = "";
    public string DavrMatni => $"{Format.Sana(Dan.Date)} — {Format.Sana(Gacha.Date)}";

    private readonly KechiktirilganIsh _yuklash;
    private HisobotDto? _natija;

    public HisobotViewModel()
    {
        _yuklash = new KechiktirilganIsh(Yukla, 300);
        OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("BarchaOperatorlar") }, .. Malumot.Operatorlar];
        TanlanganOperator = OperatorFiltri[0];
        Korsat(null);
        Til.Ozgardi += () =>
        {
            OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("BarchaOperatorlar") }, .. Malumot.Operatorlar];
            OnPropertyChanged(string.Empty);
            TanlanganOperator = null;
            TanlanganOperator = OperatorFiltri[0];
            Korsat(_natija);
        };
        Malumot.Ozgardi += () =>
        {
            // Operatorlar ro'yxati login'dan keyin keladi — filtrni qayta quramiz (tanlov Id bo'yicha saqlanadi).
            if (!OperatorFiltri.Skip(1).Select(o => o.Id).SequenceEqual(Malumot.Operatorlar.Select(o => o.Id)))
            {
                var tanlanganId = TanlanganOperator?.Id ?? 0;
                OperatorFiltri = [new Foydalanuvchi { Id = 0, ToliqIsm = Til.T("BarchaOperatorlar") }, .. Malumot.Operatorlar];
                OnPropertyChanged(nameof(OperatorFiltri));
                TanlanganOperator = OperatorFiltri.FirstOrDefault(o => o.Id == tanlanganId) ?? OperatorFiltri[0];
            }
            if (!Malumot.Kirilgan) { _natija = null; Korsat(null); }
            ExcelCommand.NotifyCanExecuteChanged();
            Rejala();
        };
    }

    private void Rejala()
    {
        if (Malumot.Kirilgan && Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Hisobotlar)) _yuklash.Rejala();
    }

    partial void OnTanlanganOperatorChanged(Foydalanuvchi? value) => Rejala();
    partial void OnDanChanged(DateTimeOffset value) { OnPropertyChanged(nameof(DavrMatni)); Rejala(); }
    partial void OnGachaChanged(DateTimeOffset value) { OnPropertyChanged(nameof(DavrMatni)); Rejala(); }
    partial void OnGuruhlashChanged(int value)
    {
        OnPropertyChanged(nameof(OperatorBoyicha));
        OnPropertyChanged(nameof(KunBoyicha));
        OnPropertyChanged(nameof(OyBoyicha));
        OnPropertyChanged(nameof(GuruhSarlavha));
        Rejala();
    }

    private HisobotGuruhi Guruh => Guruhlash switch { 1 => HisobotGuruhi.Kun, 2 => HisobotGuruhi.Oy, _ => HisobotGuruhi.Operator };

    private async Task Yukla(Func<bool> dolzarb)
    {
        var opId = TanlanganOperator is { Id: > 0 } o ? o.Id : (int?)null;
        var n = await Malumot.Api.Hisobot(DateOnly.FromDateTime(Dan.Date), DateOnly.FromDateTime(Gacha.Date), opId, Guruh);
        if (!dolzarb() || !Malumot.Kirilgan) return;
        _natija = n;
        Korsat(n);
        ExcelCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Hisobotni hozir yuklash (masalan, "Bugungi hisobot" tugmasidan keyin).</summary>
    public Task HozirYukla() => _yuklash.Bajar();

    [RelayCommand]
    private void GuruhniTanla(string i) => Guruhlash = int.Parse(i);

    [RelayCommand]
    private void TezDavr(string t)
    {
        var bugun = DateTime.Today;
        var oyBoshi = new DateTime(bugun.Year, bugun.Month, 1);
        (DateTime d, DateTime g) = t switch
        {
            "bugun" => (bugun, bugun),
            "kecha" => (bugun.AddDays(-1), bugun.AddDays(-1)),
            "hafta" => (bugun.AddDays(-6), bugun),
            "oy" => (oyBoshi, bugun),
            "otganOy" => (oyBoshi.AddMonths(-1), oyBoshi.AddDays(-1)),
            _ => (Dan.Date, Gacha.Date),
        };
        Dan = d; Gacha = g;
    }

    /// <summary>Server guruh qiymati: "yyyy-MM-dd" → dd.MM.yyyy, "yyyy-MM" → oy nomi + yil (joriy til madaniyatida), operator — ismi.</summary>
    private string GuruhNomi(string kalit) => Guruh switch
    {
        HisobotGuruhi.Kun when DateTime.TryParseExact(kalit, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var k) => Format.Sana(k),
        HisobotGuruhi.Oy when DateTime.TryParseExact(kalit, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var o) => o.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
        _ => kalit,
    };

    /// <summary>Server tartibi: guruh ichida yoqilg'i qatorlari, oxirida guruh jami (Jami = true) — qalin ko'rsatiladi.</summary>
    private void Korsat(HisobotDto? n)
    {
        Qatorlar.Clear();
        if (n is not null)
            foreach (var q in n.Qatorlar)
                Qatorlar.Add(new HisobotQatori(q.Jami ? "" : GuruhNomi(q.Guruh), q.Jami ? Til.T("Jami") : q.Yoqilgi ?? "", Format.Litr(q.Litr), Format.Pul(q.Summa),
                    Format.Pul(q.Naqd), Format.Pul(q.Plastik), Format.Pul(q.Click), q.Soni, q.Jami));

        var j = n?.Jami;
        JamiSumma = Format.Pul(j?.Summa ?? 0);
        JamiLitr = Format.Litr(j?.Litr ?? 0);
        JamiNaqd = Format.Pul(j?.Naqd ?? 0);
        JamiPlastik = Format.Pul(j?.Plastik ?? 0);
        JamiClick = Format.Pul(j?.Click ?? 0);
        JamiKamomat = Format.Pul(j?.Kamomat ?? 0);
        JamiAvans = Format.Pul(j?.Avans ?? 0);
        BekorSoni = (j?.BekorSoni ?? 0).ToString();
        foreach (var p in new[] { nameof(JamiSumma), nameof(JamiLitr), nameof(JamiNaqd), nameof(JamiPlastik), nameof(JamiClick),
                     nameof(JamiKamomat), nameof(JamiAvans), nameof(BekorSoni), nameof(DavrMatni) })
            OnPropertyChanged(p);
    }

    private bool EksportMumkin() => _natija is not null && Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Eksport);

    /// <summary>Joriy hisobot → Documents\FuelControl\Hisobotlar\*.xlsx; eksport server auditiga yoziladi.</summary>
    [RelayCommand(CanExecute = nameof(EksportMumkin))]
    private async Task Excel()
    {
        if (_natija is not { } n) return;
        string[] ustunlar = [GuruhSarlavha, Til.T("Yoqilgi"), Til.T("Litr"), Til.T("Summa"), Til.T("Naqd"), Til.T("Plastik"), Til.T("Click"),
            Til.T("Soni"), Til.T("Kamomat"), Til.T("Avans"), Til.T("BekorQilingan")];
        object?[] Qiymat(HisobotQatoriDto q, string yoqilgi) =>
            [GuruhNomi(q.Guruh), yoqilgi, q.Litr, q.Summa, q.Naqd, q.Plastik, q.Click, q.Soni,
             q.Jami ? q.Kamomat : null, q.Jami ? q.Avans : null, q.Jami ? q.BekorSoni : null];

        var qatorlar = n.Qatorlar.Select(q => (Qiymat(q, q.Jami ? Til.T("Jami") : q.Yoqilgi ?? ""), q.Jami));
        var davr = $"{Dan.Date:yyyy-MM-dd}_{Gacha.Date:yyyy-MM-dd}";
        var opNomi = TanlanganOperator is { Id: > 0 } o ? o.ToliqIsm : Til.T("BarchaOperatorlar");
        try
        {
            var fayl = ExcelEksport.Saqla("Hisobotlar", $"hisobot-{Guruh.ToString().ToLowerInvariant()}-{davr}.xlsx",
                Til.T("Hisobotlar"), $"{DavrMatni} · {opNomi} · {GuruhSarlavha}", ustunlar, qatorlar,
                Qiymat(n.Jami with { Guruh = Til.T("Jami") }, ""));
            await Malumot.EksportniYoz("Hisobot", $"{DavrMatni}, {opNomi}, {Guruh} — {System.IO.Path.GetFileName(fayl)}");
            Bildirish.Malumot($"{Til.T("FaylSaqlandi")}: {fayl}");
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            Bildirish.Xato(e.Message);
        }
    }
}
