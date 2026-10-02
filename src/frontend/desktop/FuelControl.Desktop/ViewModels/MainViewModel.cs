using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FuelControl.Desktop.Models;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public LoginViewModel Login { get; }
    public BoshqaruvViewModel Boshqaruv { get; } = new();
    public SotuvViewModel Sotuv { get; } = new();
    public SmenalarViewModel Smenalar { get; } = new();
    public HisobotViewModel Hisobot { get; } = new();
    public OperatorlarViewModel Operatorlar { get; } = new();
    public SozlamalarViewModel Sozlamalar { get; } = new();
    public AuditViewModel Audit { get; } = new();
    public SotuvTahrirViewModel SotuvTahrir { get; } = new();

    [ObservableProperty] private bool _kirilgan;
    [ObservableProperty] private Foydalanuvchi _joriy = Malumot.JoriyFoydalanuvchi;

    // Umumiy xabar dialogi (Bildirish.Xato / Bildirish.Malumot)
    [ObservableProperty] private bool _bildirishOchiq;
    [ObservableProperty] private string _bildirishSarlavha = "";
    [ObservableProperty] private string _bildirishMatni = "";
    [ObservableProperty] private bool _bildirishXato;

    [RelayCommand] private void BildirishniYop() => BildirishOchiq = false;

    /// <summary>Yon menyudagi indikator: SignalR ulanishi holati.</summary>
    public bool AloqaBor => Malumot.AloqaBor;
    public bool AloqaYoq => !Malumot.AloqaBor;

    // Yon menyu: yig'ilgan rejimda faqat ikonkalar qoladi
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MenyuKengligi), nameof(MenyuOchiq))]
    private bool _menyuYigilgan;
    public double MenyuKengligi => MenyuYigilgan ? 92 : 252;
    public bool MenyuOchiq => !MenyuYigilgan;

    [ObservableProperty] private bool _qorongiRejim;

    public Til Til => Til.Joriy;
    public string JoriyRolNomi => Joriy.RolNomi;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoshqaruvSahifasi), nameof(SotuvSahifasi), nameof(SmenalarSahifasi),
        nameof(HisobotSahifasi), nameof(OperatorlarSahifasi), nameof(SozlamalarSahifasi), nameof(AuditSahifasi))]
    private int _sahifa;

    public bool BoshqaruvSahifasi => Sahifa == 0;
    public bool SotuvSahifasi => Sahifa == 1;
    public bool SmenalarSahifasi => Sahifa == 2;
    public bool HisobotSahifasi => Sahifa == 3;
    public bool OperatorlarSahifasi => Sahifa == 4;
    public bool SozlamalarSahifasi => Sahifa == 5;
    public bool AuditSahifasi => Sahifa == 6;

    // Ruxsatlarga qarab menyu bo'limlari
    public bool KoradiBoshqaruv => Joriy.Bor(Ruxsat.Boshqaruv);
    public bool KoradiSotuv => Joriy.Bor(Ruxsat.SotuvKiritish);
    public bool KoradiSmenalar => Joriy.Bor(Ruxsat.Smenalar);
    public bool KoradiHisobot => Joriy.Bor(Ruxsat.Hisobotlar);
    public bool KoradiOperatorlar => Joriy.Bor(Ruxsat.Operatorlar);
    public bool KoradiAudit => Joriy.Bor(Ruxsat.Audit);
    public bool KoradiSozlamalar => Joriy.Bor(Ruxsat.Sozlamalar);
    public bool KoradiEksport => Joriy.Bor(Ruxsat.Eksport);
    public bool KoradiAvans => Joriy.Bor(Ruxsat.AvansBerish);
    public bool KoradiBekorQilish => Joriy.Bor(Ruxsat.SotuvBekorQilish);
    public bool KoradiSotuvTahrirlash => Joriy.Bor(Ruxsat.SotuvTahrirlash);

    public MainViewModel()
    {
        Login = new LoginViewModel(f =>
        {
            Joriy = f;
            OnPropertyChanged(nameof(JoriyRolNomi));
            RuxsatlarniYangila();
            Sahifa = BirinchiSahifa();
            Kirilgan = true;
        });
        Malumot.Ozgardi += RuxsatlarniYangila;
        Malumot.AloqaOzgardi += () => { OnPropertyChanged(nameof(AloqaBor)); OnPropertyChanged(nameof(AloqaYoq)); };
        Malumot.MajburiyChiqish += sabab => _ = ChiqishAsync(sabab);
        Bildirish.Korsatildi += (sarlavha, matn, xato) =>
        {
            BildirishSarlavha = sarlavha; BildirishMatni = matn; BildirishXato = xato;
            BildirishOchiq = true;
        };
        Til.Ozgardi += () => OnPropertyChanged(string.Empty);
    }

    private async System.Threading.Tasks.Task ChiqishAsync(string? sabab)
    {
        if (!Kirilgan) return;
        Kirilgan = false;
        SotuvTahrir.Ochiq = false;
        await Malumot.Chiqish();
        Joriy = Malumot.JoriyFoydalanuvchi;
        Login.Tozala(sabab);
    }

    /// <summary>Ruxsati bor birinchi bo'lim — kirgandan keyin shu ochiladi.</summary>
    private int BirinchiSahifa()
    {
        if (KoradiBoshqaruv) return 0;
        if (KoradiSotuv) return 1;
        if (KoradiSmenalar) return 2;
        if (KoradiHisobot) return 3;
        if (KoradiOperatorlar) return 4;
        if (KoradiAudit) return 6;
        return 5;
    }

    private void RuxsatlarniYangila()
    {
        foreach (var n in new[] { nameof(KoradiBoshqaruv), nameof(KoradiSotuv), nameof(KoradiSmenalar), nameof(KoradiHisobot),
                     nameof(KoradiOperatorlar), nameof(KoradiAudit), nameof(KoradiSozlamalar),
                     nameof(KoradiEksport), nameof(KoradiAvans), nameof(KoradiBekorQilish), nameof(KoradiSotuvTahrirlash) })
            OnPropertyChanged(n);
        OnPropertyChanged(nameof(JoriyRolNomi));
        BugungiHisobotCommand.NotifyCanExecuteChanged();
        // Ochiq sahifaga ruxsat olib tashlangan bo'lsa — ruxsati bor birinchi sahifaga o'tamiz.
        if (Kirilgan && !SahifaRuxsatli(Sahifa)) Sahifa = BirinchiSahifa();
    }

    private bool SahifaRuxsatli(int s) => s switch
    {
        0 => KoradiBoshqaruv, 1 => KoradiSotuv, 2 => KoradiSmenalar, 3 => KoradiHisobot,
        4 => KoradiOperatorlar, 5 => KoradiSozlamalar, 6 => KoradiAudit, _ => false,
    };

    [RelayCommand]
    private void SahifaniOch(string indeks) => Sahifa = int.Parse(indeks);

    /// <summary>Boshqaruv → "Bugungi hisobot": Hisobot sahifasi bugungi davr bilan.</summary>
    [RelayCommand(CanExecute = nameof(KoradiHisobot))]
    private async System.Threading.Tasks.Task BugungiHisobot()
    {
        Hisobot.TezDavrCommand.Execute("bugun");
        Sahifa = 3;
        await Hisobot.HozirYukla();
    }

    [RelayCommand]
    private void MenyuniYig() => MenyuYigilgan = !MenyuYigilgan;

    [RelayCommand]
    private void TilniTanla(string kod) => Til.Joriy.Tanla(kod);

    [RelayCommand]
    private void KeyingiTil() => Til.Joriy.Keyingi();

    [RelayCommand]
    private void RejimniAlmashtir()
    {
        QorongiRejim = !QorongiRejim;
        if (Application.Current is { } app)
            app.RequestedThemeVariant = QorongiRejim ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    [RelayCommand]
    private System.Threading.Tasks.Task Chiqish() => ChiqishAsync(null);
}
