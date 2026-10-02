using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

/// <summary>
/// Kirish: login + parol (yoki PIN). Foydalanuvchilar ro'yxati serverdan olinmaydi (kirishdan oldin token yo'q);
/// shu kompyuterda oldin kirganlar eslab qolinadi — ulardan operator tanlansa PIN klaviaturasi chiqadi.
/// </summary>
public partial class LoginViewModel : ObservableObject
{
    private readonly Action<Foydalanuvchi> _kirdi;

    public List<OxirgiKirgan> OxirgiKirganlar { get; private set; } = Sozlama.Joriy.OxirgiKirganlar.ToList();
    public bool OxirgiKirganlarBor => OxirgiKirganlar.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Operatormi))]
    private string _login = Sozlama.Joriy.OxirgiKirganlar.FirstOrDefault()?.Login ?? "";

    [ObservableProperty] private string _pin = "";
    [ObservableProperty] private string _parol = "";
    [ObservableProperty] private string _xato = "";
    [ObservableProperty] private string _serverManzili = Sozlama.Joriy.ServerManzili;
    [ObservableProperty] private bool _serverOchiq;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(KirishCommand))]
    private bool _band;

    /// <summary>PIN klaviaturasi — shu kompyuterda operator sifatida kirgan login uchun.</summary>
    public bool Operatormi => OxirgiKirganlar.Any(k => k.Rol == Rol.Operator && k.Login.Equals(Login.Trim(), StringComparison.OrdinalIgnoreCase));

    public string ServerIzohi => Til.F("ServerIzoh", ServerManzili);

    public LoginViewModel(Action<Foydalanuvchi> kirdi)
    {
        _kirdi = kirdi;
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
    }

    partial void OnServerManziliChanged(string value) => OnPropertyChanged(nameof(ServerIzohi));

    [RelayCommand] private void LoginniTanla(OxirgiKirgan k) { Login = k.Login; Pin = ""; Parol = ""; Xato = ""; }
    [RelayCommand] private void ServerniOchYop() => ServerOchiq = !ServerOchiq;

    private bool KirishMumkin() => !Band;

    [RelayCommand(CanExecute = nameof(KirishMumkin))]
    private async Task Kirish()
    {
        if (Login.Trim().Length == 0) { Xato = Til.T("LoginniKiriting"); return; }
        if (Operatormi && Pin.Length < 4) { Xato = Til.T("Xato_Pin"); return; }
        if (!Operatormi && Parol.Length == 0) { Xato = Til.T("ParolniKiriting"); return; }

        Xato = Til.T("Yuklanmoqda");
        Band = true;
        try
        {
            var f = await Malumot.Kirish(ServerManzili, Login, Operatormi ? Pin : Parol);
            var s = Sozlama.Joriy;
            s.ServerManzili = Malumot.Api.Manzil;
            s.KirganniEslab(new OxirgiKirgan(f.Login, f.ToliqIsm, f.Rol));
            OxirgiKirganlar = s.OxirgiKirganlar.ToList();
            OnPropertyChanged(nameof(OxirgiKirganlar));
            OnPropertyChanged(nameof(OxirgiKirganlarBor));
            Xato = "";
            _kirdi(f);
        }
        catch (ApiXatosi e)
        {
            Xato = e.Message;
            Pin = "";
        }
        finally
        {
            Band = false;
        }
    }

    [RelayCommand]
    private void Raqam(string r)
    {
        if (r == "⌫") { if (Pin.Length > 0) Pin = Pin[..^1]; return; }
        if (Pin.Length < 6) Pin += r;
    }

    public void Tozala(string? xabar = null) { Pin = ""; Parol = ""; Xato = xabar ?? ""; }
}
