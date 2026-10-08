using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>Audit qatori: tur belgisi rangi (badge klassi) va bosh harflar.</summary>
public sealed record AuditQatori(AuditYozuvi Y, string Harflar, string BelgiKlassi)
{
    public string Vaqt => Format.Vaqt(Y.Vaqt);
    public string Sana => Format.Sana(Y.Vaqt);
}

/// <summary>Audit jurnali: qidiruv va tur filtri (smena / nasiya / xarajat / bak / tuzatish). Keshdagi oxirgi 500 yozuvdan.</summary>
public partial class AuditViewModel : ObservableObject
{
    public ObservableCollection<AuditQatori> Yozuvlar { get; } = new();

    [ObservableProperty] private string _qidiruv = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TurHammasi), nameof(TurSmena), nameof(TurNasiya), nameof(TurXarajat), nameof(TurBak), nameof(TurTuzatish))]
    private string _tur = "";

    public bool TurHammasi => Tur == "";
    public bool TurSmena => Tur == "smena";
    public bool TurNasiya => Tur == "nasiya";
    public bool TurXarajat => Tur == "xarajat";
    public bool TurBak => Tur == "bak";
    public bool TurTuzatish => Tur == "tuzatish";

    public bool BoshQator => Yozuvlar.Count == 0;

    public AuditViewModel()
    {
        Filtrla();
        Malumot.Ozgardi += Filtrla;
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
    }

    partial void OnQidiruvChanged(string value) => Filtrla();
    partial void OnTurChanged(string value) => Filtrla();

    [RelayCommand] private void TurniTanla(string t) => Tur = t;

    /// <summary>Tur → belgi rangi (dizayndagi kabi): smena ko'k, nasiya olov, xarajat sariq, bak yashil, tuzatish qizil, qolgani kulrang.</summary>
    public static string BelgiKlassi(string tur) => tur switch
    {
        "smena" => "kok",
        "nasiya" => "olov",
        "xarajat" => "sariq",
        "bak" => "yashil",
        "tuzatish" => "qizil",
        _ => "kulrang",
    };

    private void Filtrla()
    {
        Yozuvlar.Clear();
        var q = Qidiruv.Trim();
        foreach (var y in Malumot.Audit.Where(y => (Tur.Length == 0 || y.Tur == Tur) && (q.Length == 0 ||
                     y.Kim.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                     y.Amal.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                     y.Tafsilot.Contains(q, StringComparison.OrdinalIgnoreCase))).Take(300))
            Yozuvlar.Add(new AuditQatori(y, Format.BoshHarflar(y.Kim), BelgiKlassi(y.Tur)));
        OnPropertyChanged(nameof(BoshQator));
    }
}
