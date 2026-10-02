using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using FuelControl.Contracts.Dto;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Bitta operatorning hisob-varaqasi: maosh, avans, kamomat, qoldiq.</summary>
public partial class OperatorKartasi : ObservableObject
{
    public Foydalanuvchi Operator { get; }
    public string Maosh => Format.Pul(Operator.OylikMaosh);
    public string OyAvans { get; private set; } = "";
    public string OyKamomat { get; private set; } = "";
    public string OyOrtiqcha { get; private set; } = "";
    public string OySavdo { get; private set; } = "";
    public string OySmenalar { get; private set; } = "";
    public long Qoldiq { get; private set; }
    public string QoldiqMatni => Format.Farq(Qoldiq);
    public bool QoldiqManfiy => Qoldiq < 0;
    public bool SmenadaMi { get; private set; }
    [ObservableProperty] private bool _tanlangan;

    public OperatorKartasi(Foydalanuvchi op) { Operator = op; Yangila(); }

    public void Yangila()
    {
        var oyBoshi = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var h = Malumot.Harakatlar.Where(x => x.Operator.Id == Operator.Id).ToList();
        var oy = h.Where(x => x.Sana >= oyBoshi).ToList();
        OyAvans = Format.Pul(-oy.Where(x => x.Turi == HarakatTuri.Avans).Sum(x => x.Summa));
        OyKamomat = Format.Pul(-oy.Where(x => x.Turi == HarakatTuri.Kamomat).Sum(x => x.Summa));
        OyOrtiqcha = Format.Pul(oy.Where(x => x.Turi == HarakatTuri.Ortiqcha).Sum(x => x.Summa));
        Qoldiq = h.Sum(x => x.Summa);
        // Oylik savdo va smenalar soni — server hisoblaydi (keshda faqat bugungi sotuvlar bor).
        var stat = Malumot.OyStatistikasi.GetValueOrDefault(Operator.Id);
        OySmenalar = stat.Smenalar.ToString();
        OySavdo = Format.Pul(stat.Savdo);
        SmenadaMi = Malumot.Smenalar.Any(s => s.Operator.Id == Operator.Id && s.Ochiqmi);
        OnPropertyChanged(string.Empty);
    }
}

/// <summary>Hisob-varaqa qatori (davr ichidagi harakat, yig'ilib boruvchi qoldiq bilan).</summary>
public sealed record VaraqaQatori(string Sana, string Turi, string Izoh, string Summa, bool Musbat, string Qoldiq);

public partial class OperatorlarViewModel : ObservableObject
{
    public List<OperatorKartasi> Kartalar { get; private set; } = Malumot.Operatorlar.Select(o => new OperatorKartasi(o)).ToList();
    public ObservableCollection<HisobHarakati> Harakatlar { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TanlanganBormi))]
    private OperatorKartasi? _tanlangan;

    // ---- Avans / to'lov dialogi
    [ObservableProperty] private bool _avansOchiq;
    [ObservableProperty] private string _avansSumma = "";
    [ObservableProperty] private string _avansIzoh = "";
    [ObservableProperty] private int _avansTuri; // 0 avans, 1 to'lov (maosh berish)

    public bool TanlanganBormi => Tanlangan is not null;
    public bool AvansTanlangan => AvansTuri == 0;
    public bool TolovTanlangan => AvansTuri == 1;

    // ---- Hisob-varaqa dialogi
    [ObservableProperty] private bool _varaqaOchiq;
    [ObservableProperty] private DateTime _varaqaOy = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public ObservableCollection<VaraqaQatori> VaraqaQatorlari { get; } = new();
    public string VaraqaDavr => VaraqaOy.ToString("MMMM yyyy");
    public string VaraqaBoshlangich { get; private set; } = "";
    public string VaraqaKirim { get; private set; } = "";
    public string VaraqaChiqim { get; private set; } = "";
    public string VaraqaYakuniy { get; private set; } = "";
    public bool VaraqaYakuniyManfiy { get; private set; }
    [ObservableProperty] private string _varaqaXabar = "";

    public OperatorlarViewModel()
    {
        if (Kartalar.Count > 0) OperatorniTanla(Kartalar[0]);
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
        Malumot.AloqaOzgardi += () => { AvansniOchCommand.NotifyCanExecuteChanged(); AvansniSaqlaCommand.NotifyCanExecuteChanged(); };
        Malumot.Ozgardi += () =>
        {
            // Operatorlar ro'yxati o'zgargan bo'lsa (login, yangi operator) — kartalarni qayta qurish
            if (!Kartalar.Select(k => k.Operator).SequenceEqual(Malumot.Operatorlar))
            {
                var tanlanganId = Tanlangan?.Operator.Id;
                Kartalar = Malumot.Operatorlar.Select(o => new OperatorKartasi(o)).ToList();
                OnPropertyChanged(nameof(Kartalar));
                var k = Kartalar.FirstOrDefault(x => x.Operator.Id == tanlanganId) ?? Kartalar.FirstOrDefault();
                if (k is not null) OperatorniTanla(k);
                else { Tanlangan = null; Harakatlar.Clear(); }
            }
            foreach (var k in Kartalar) k.Yangila();
            VaraqaniEksportCommand.NotifyCanExecuteChanged();
            if (Tanlangan is not null) HarakatlarniYukla(Tanlangan);
            if (VaraqaOchiq) VaraqaniHisobla();
        };
    }

    private static bool AloqaBor() => Malumot.AloqaBor;

    private void HarakatlarniYukla(OperatorKartasi k)
    {
        Harakatlar.Clear();
        foreach (var h in Malumot.Harakatlar.Where(h => h.Operator.Id == k.Operator.Id)) Harakatlar.Add(h);
    }

    [RelayCommand]
    private void OperatorniTanla(OperatorKartasi k)
    {
        foreach (var x in Kartalar) x.Tanlangan = x == k;
        Tanlangan = k;
        HarakatlarniYukla(k);
    }

    // ================= Avans / to'lov =================
    [RelayCommand(CanExecute = nameof(AloqaBor))] private void AvansniOch() { AvansSumma = ""; AvansIzoh = ""; AvansTuri = 0; AvansOchiq = true; }
    [RelayCommand] private void AvansniYop() => AvansOchiq = false;

    [RelayCommand]
    private void AvansTuriniTanla(string t)
    {
        AvansTuri = int.Parse(t);
        OnPropertyChanged(nameof(AvansTanlangan));
        OnPropertyChanged(nameof(TolovTanlangan));
    }

    [RelayCommand(CanExecute = nameof(AloqaBor))]
    private async Task AvansniSaqla()
    {
        if (Tanlangan is null) return;
        var summa = long.TryParse(new string(AvansSumma.Where(char.IsDigit).ToArray()), out var v) ? v : 0;
        if (summa <= 0) return;
        var izoh = AvansIzoh.Length > 0 ? AvansIzoh : (AvansTuri == 0 ? Til.T("Avans") : Til.T("MaoshBerildi"));
        try
        {
            await Malumot.HarakatYoz(Tanlangan.Operator.Id,
                new HarakatYaratishDto(AvansTuri == 0 ? HarakatTuri.Avans : HarakatTuri.Tolov, summa, izoh));
            AvansOchiq = false;
        }
        catch (ApiXatosi e)
        {
            Bildirish.Xato(e.Message);
        }
    }

    // ================= Hisob-varaqa =================
    [RelayCommand]
    private void VaraqaniOch()
    {
        if (Tanlangan is null) return;
        VaraqaOy = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        VaraqaXabar = "";
        VaraqaniHisobla();
        VaraqaOchiq = true;
    }

    [RelayCommand] private void VaraqaniYop() => VaraqaOchiq = false;
    [RelayCommand] private void OldingiOy() { VaraqaOy = VaraqaOy.AddMonths(-1); VaraqaniHisobla(); }
    [RelayCommand] private void KeyingiOy() { VaraqaOy = VaraqaOy.AddMonths(1); VaraqaniHisobla(); }

    private void VaraqaniHisobla()
    {
        VaraqaQatorlari.Clear();
        if (Tanlangan is null) return;
        var oxiri = VaraqaOy.AddMonths(1);
        var hamma = Malumot.Harakatlar.Where(h => h.Operator.Id == Tanlangan.Operator.Id).OrderBy(h => h.Sana).ToList();
        long boshlangich = hamma.Where(h => h.Sana < VaraqaOy).Sum(h => h.Summa);
        long qoldiq = boshlangich, kirim = 0, chiqim = 0;
        foreach (var h in hamma.Where(h => h.Sana >= VaraqaOy && h.Sana < oxiri))
        {
            qoldiq += h.Summa;
            if (h.Summa > 0) kirim += h.Summa; else chiqim += -h.Summa;
            VaraqaQatorlari.Add(new VaraqaQatori(Format.Sana(h.Sana), h.TuriNomi, h.Izoh, Format.Farq(h.Summa), h.Summa > 0, Format.Farq(qoldiq)));
        }
        VaraqaBoshlangich = Format.Farq(boshlangich);
        VaraqaKirim = Format.Pul(kirim);
        VaraqaChiqim = Format.Pul(chiqim);
        VaraqaYakuniy = Format.Farq(qoldiq);
        VaraqaYakuniyManfiy = qoldiq < 0;
        foreach (var n in new[] { nameof(VaraqaDavr), nameof(VaraqaBoshlangich), nameof(VaraqaKirim), nameof(VaraqaChiqim), nameof(VaraqaYakuniy), nameof(VaraqaYakuniyManfiy) })
            OnPropertyChanged(n);
    }

    private static bool EksportMumkin() => Malumot.JoriyFoydalanuvchi.Bor(Ruxsat.Eksport);

    /// <summary>Hisob-varaqa → Documents\FuelControl\Hisob-varaqa\*.xlsx (summalar son sifatida); eksport auditga yoziladi.</summary>
    [RelayCommand(CanExecute = nameof(EksportMumkin))]
    private async Task VaraqaniEksport()
    {
        if (Tanlangan is null) return;
        var op = Tanlangan.Operator;
        var oxiri = VaraqaOy.AddMonths(1);
        var hamma = Malumot.Harakatlar.Where(h => h.Operator.Id == op.Id).OrderBy(h => h.Sana).ToList();
        long boshlangich = hamma.Where(h => h.Sana < VaraqaOy).Sum(h => h.Summa);
        long qoldiq = boshlangich, kirim = 0, chiqim = 0;
        var qatorlar = new List<(object?[], bool)> { ([Til.T("BoshlangichQoldiq"), null, null, null, boshlangich], true) };
        foreach (var h in hamma.Where(h => h.Sana >= VaraqaOy && h.Sana < oxiri))
        {
            qoldiq += h.Summa;
            if (h.Summa > 0) kirim += h.Summa; else chiqim += -h.Summa;
            qatorlar.Add(([Format.Sana(h.Sana), h.TuriNomi, h.Izoh, h.Summa, qoldiq], false));
        }
        qatorlar.Add(([Til.T("Jami_Kirim"), null, null, kirim, null], true));
        qatorlar.Add(([Til.T("Jami_Chiqim"), null, null, -chiqim, null], true));

        try
        {
            var fayl = ExcelEksport.Saqla("Hisob-varaqa", $"{op.Login}-{VaraqaOy:yyyy-MM}.xlsx",
                Til.T("HisobVaraqa"), $"{op.ToliqIsm} · {VaraqaDavr}",
                [Til.T("Sana"), Til.T("Turi"), Til.T("Izoh"), Til.T("Summa"), Til.T("Qoldiq")],
                qatorlar, [Til.T("YakuniyQoldiq"), null, null, null, qoldiq]);
            VaraqaXabar = $"{Til.T("FaylSaqlandi")}: {fayl}";
            await Malumot.EksportniYoz("Hisob-varaqa", $"{op.ToliqIsm}, {VaraqaOy:yyyy-MM} — {Path.GetFileName(fayl)}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            VaraqaXabar = e.Message;
        }
    }
}
