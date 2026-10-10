using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using FuelControl.Desktop.ViewModels;

namespace FuelControl.Desktop.Views;

public partial class DialoglarView : UserControl
{
    private NasiyaDialogVM? _nasiya;

    public DialoglarView()
    {
        InitializeComponent();
        // Nasiya dialogi (§8.8): ism / telefon / mashina maydonlarida takliflar bilan klaviatura (↑/↓, Enter, Esc).
        // Tunnel — ajdod maydonning o'z ishlovchilaridan (EnterTartib, TelefonMaydon) oldin eshitadi.
        NasiyaParda.AddHandler(KeyDownEvent, NasiyaKey, RoutingStrategies.Tunnel);
        // Fokus taklif maydonlaridan boshqa joyga o'tsa (masalan, "Saqlash" bosildi) — ro'yxat yopiladi; u ustma-ust chizilgani uchun hech narsa siljimaydi.
        NasiyaParda.AddHandler(GotFocusEvent, (_, e) =>
        {
            if (_nasiya is { } vm && e.Source is Visual v && !TaklifMaydonida(v)) vm.TakliflarniYop();
        }, RoutingStrategies.Bubble);
        // Qarz qaytdi: qator bosilib turganda kelgan qidiruv natijasi ro'yxatni almashtirmaydi (bosish yo'qolmasin)
        QaytishRoyxat.AddHandler(PointerPressedEvent, (_, _) => (QaytishRoyxat.DataContext as QaytishDialogVM)?.RoyxatniUshla(true), RoutingStrategies.Tunnel, handledEventsToo: true);
        void QoyibYubor() => Avalonia.Threading.Dispatcher.UIThread.Post(() => (QaytishRoyxat.DataContext as QaytishDialogVM)?.RoyxatniUshla(false), Avalonia.Threading.DispatcherPriority.Background);
        QaytishRoyxat.AddHandler(PointerReleasedEvent, (_, _) => QoyibYubor(), RoutingStrategies.Tunnel, handledEventsToo: true);
        QaytishRoyxat.AddHandler(PointerCaptureLostEvent, (_, _) => QoyibYubor(), RoutingStrategies.Bubble, handledEventsToo: true);
        NasiyaParda.DataContextChanged += (_, _) =>
        {
            if (_nasiya is not null) _nasiya.PropertyChanged -= NasiyaOzgardi;
            _nasiya = NasiyaParda.DataContext as NasiyaDialogVM;
            if (_nasiya is not null) _nasiya.PropertyChanged += NasiyaOzgardi;
        };
    }

    private static bool Ichida(Visual v, Visual ota) => v == ota || v.GetVisualAncestors().Contains(ota);
    private bool TaklifMaydonida(Visual v) => Ichida(v, NasiyaIsm) || Ichida(v, NasiyaTelefon) || Ichida(v, NasiyaMashina);

    private static void Fokusla(Control c)
    {
        c.Focus(NavigationMethod.Tab);
        if (c is TextBox t) t.SelectAll();
    }

    /// <summary>Taklif tanlangach — birinchi bo'sh maydon (odatda Summa).</summary>
    private Control BirinchiBosh(NasiyaDialogVM vm) =>
        Services.Format.TelefonRaqamlari(vm.Telefon).Length == 0 ? NasiyaTelefon
        : string.IsNullOrWhiteSpace(vm.MashinaRaqami) ? NasiyaMashina
        : string.IsNullOrWhiteSpace(vm.Summa) ? NasiyaSumma
        : NasiyaIzoh;

    private void NasiyaKey(object? sender, KeyEventArgs e)
    {
        if (_nasiya is not { } vm || e.Source is not Visual v) return;
        var ism = Ichida(v, NasiyaIsm);
        if (!ism && !Ichida(v, NasiyaTelefon) && !Ichida(v, NasiyaMashina)) return;
        switch (e.Key)
        {
            case Key.Down:
                if (vm.TaklifniSur(1)) e.Handled = true;
                break;
            case Key.Up:
                if (vm.TaklifniSur(-1)) e.Handled = true;
                break;
            case Key.Escape:
                // Ro'yxat yopiladi, yozilgan matn o'zgarmaydi
                if (vm.TakliflarKorinsin) { vm.TakliflarniYop(); e.Handled = true; }
                break;
            case Key.Enter or Key.Return when e.KeyModifiers == KeyModifiers.None:
                if (vm.BelgilanganniTanla()) { e.Handled = true; Fokusla(BirinchiBosh(vm)); }
                else if (ism)
                {
                    // Ism bo'sh — fokus joyida; aks holda (yangi mijoz yoki ro'yxat yopiq) — Telefon
                    e.Handled = true;
                    if (!string.IsNullOrWhiteSpace(vm.MijozIsmi)) { vm.TakliflarniYop(); Fokusla(NasiyaTelefon); }
                }
                else vm.TakliflarniYop();   // telefon / mashina: keyingi maydonga EnterTartib o'tkazadi
                break;
        }
    }

    /// <summary>Saqlashda xato: fokus xato maydonga, xato matni (tugmalar yonida) ko'rinadigan joyga suriladi.</summary>
    private void NasiyaOzgardi(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NasiyaDialogVM.XatoMaydon) || _nasiya is not { XatoMaydon: { Length: > 0 } m }) return;
        Control? c = m switch { "ism" => NasiyaIsm, "telefon" => NasiyaTelefon, "summa" => NasiyaSumma, "muddat" => NasiyaMuddat, _ => null };
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (c is not null) Fokusla(c);
            NasiyaXato.BringIntoView();
        });
    }
}
