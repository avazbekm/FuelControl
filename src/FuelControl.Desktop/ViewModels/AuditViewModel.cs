using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Audit jurnali va bekor qilingan sotuvlar.</summary>
public partial class AuditViewModel : ObservableObject
{
    public ObservableCollection<AuditYozuvi> Yozuvlar { get; } = new();
    public ObservableCollection<Sotuv> BekorQilinganlar { get; } = new();

    [ObservableProperty] private string _qidiruv = "";
    [ObservableProperty] private int _bolim; // 0 jurnal, 1 bekor qilinganlar
    public bool JurnalBolimi => Bolim == 0;
    public bool BekorBolimi => Bolim == 1;

    public AuditViewModel()
    {
        Filtrla();
        Malumot.Ozgardi += () =>
        {
            Filtrla();
            BekorQilinganlar.Clear();
            foreach (var s in Malumot.BekorQilinganlar) BekorQilinganlar.Add(s);
        };
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
    }

    partial void OnQidiruvChanged(string value) => Filtrla();
    partial void OnBolimChanged(int value)
    {
        OnPropertyChanged(nameof(JurnalBolimi));
        OnPropertyChanged(nameof(BekorBolimi));
    }

    [RelayCommand] private void BolimniTanla(string i) => Bolim = int.Parse(i);

    private void Filtrla()
    {
        Yozuvlar.Clear();
        var q = Qidiruv.Trim();
        foreach (var y in Malumot.Audit.Where(y => q.Length == 0 ||
                     y.Kim.Contains(q, System.StringComparison.OrdinalIgnoreCase) ||
                     y.Amal.Contains(q, System.StringComparison.OrdinalIgnoreCase) ||
                     y.Tafsilot.Contains(q, System.StringComparison.OrdinalIgnoreCase)).Take(300))
            Yozuvlar.Add(y);
    }
}
