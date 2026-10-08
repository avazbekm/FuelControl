using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// Telefon maydoni (§8.1): ctl:TelefonMaydon.Yoqilgan="True". Matn doim "+998 " bilan boshlanadi (o'chirib bo'lmaydi), foydalanuvchi
/// 9 raqam yozadi, bo'shliqlar o'zi qo'yiladi: "+998 XX XXX XX XX".
/// Raqam yozish, Backspace va Delete shu yerda (tunnel bosqichida) bajariladi — kursor raqamlar bo'yicha joyida qoladi.
/// Joylashtirish va dasturdan o'rnatilgan qiymat Text o'zgarishida normallashtiriladi.
/// </summary>
public static class TelefonMaydon
{
    public static readonly AttachedProperty<bool> YoqilganProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Yoqilgan", typeof(TelefonMaydon));

    public static bool GetYoqilgan(TextBox t) => t.GetValue(YoqilganProperty);
    public static void SetYoqilgan(TextBox t, bool v) => t.SetValue(YoqilganProperty, v);

    private static readonly AttachedProperty<bool> IshlanmoqdaProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Ishlanmoqda", typeof(TelefonMaydon));

    static TelefonMaydon()
    {
        YoqilganProperty.Changed.AddClassHandler<TextBox>((t, e) =>
        {
            if (e.NewValue is not true) return;
            t.PropertyChanged += (_, a) => { if (a.Property == TextBox.TextProperty) Normallashtir(t); };
            t.AddHandler(InputElement.TextInputEvent, MatnKiritildi, RoutingStrategies.Tunnel);
            t.AddHandler(InputElement.KeyDownEvent, TugmaBosildi, RoutingStrategies.Tunnel);
            t.GotFocus += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                if (t.CaretIndex < Format.TelefonPrefiks.Length) t.CaretIndex = (t.Text ?? "").Length;
            });
            Normallashtir(t);
        });
    }

    /// <summary>Matndagi pozitsiyagacha bo'lgan abonent raqamlari soni (prefiks hisobga olinmaydi).</summary>
    private static int RaqamIndeksi(string matn, int pozitsiya)
    {
        pozitsiya = Math.Clamp(pozitsiya, 0, matn.Length);
        var n = 0;
        for (int i = Format.TelefonPrefiks.Length; i < pozitsiya; i++) if (char.IsDigit(matn[i])) n++;
        return n;
    }

    /// <summary>n-raqamdan keyingi matn pozitsiyasi.</summary>
    private static int Pozitsiya(string matn, int raqamlar)
    {
        var joy = Format.TelefonPrefiks.Length;
        for (int n = 0; joy < matn.Length && n < raqamlar; joy++) if (char.IsDigit(matn[joy])) n++;
        return joy;
    }

    private static void Ornat(TextBox t, string raqamlar, int kursorRaqam)
    {
        raqamlar = raqamlar.Length > 9 ? raqamlar[..9] : raqamlar;
        var matn = Format.TelefonKiritish(raqamlar);
        t.SetValue(IshlanmoqdaProperty, true);
        try { t.Text = matn; }
        finally { t.SetValue(IshlanmoqdaProperty, false); }
        t.ClearSelection();
        t.CaretIndex = Pozitsiya(matn, Math.Min(kursorRaqam, raqamlar.Length));
    }

    private static (int Bosh, int Oxir) Tanlov(TextBox t, string matn)
    {
        int a = Math.Min(t.SelectionStart, t.SelectionEnd), b = Math.Max(t.SelectionStart, t.SelectionEnd);
        return (RaqamIndeksi(matn, a), RaqamIndeksi(matn, b));
    }

    private static void MatnKiritildi(object? sender, TextInputEventArgs e)
    {
        if (sender is not TextBox t) return;
        e.Handled = true; // raqam bo'lmagan belgilar yozilmaydi
        var yangi = new string((e.Text ?? "").Where(char.IsDigit).ToArray());
        if (yangi.Length == 0) return;
        var matn = t.Text ?? Format.TelefonPrefiks;
        var r = Format.TelefonRaqamlari(matn);
        var (bosh, oxir) = Tanlov(t, matn);
        if (bosh == oxir) bosh = oxir = RaqamIndeksi(matn, t.CaretIndex);
        bosh = Math.Min(bosh, r.Length); oxir = Math.Min(oxir, r.Length);
        if (r.Length - (oxir - bosh) >= 9) return; // to'lgan
        Ornat(t, r[..bosh] + yangi + r[oxir..], bosh + yangi.Length);
    }

    private static void TugmaBosildi(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox t || e.Key is not (Key.Back or Key.Delete)) return;
        e.Handled = true;
        var matn = t.Text ?? Format.TelefonPrefiks;
        var r = Format.TelefonRaqamlari(matn);
        var (bosh, oxir) = Tanlov(t, matn);
        bosh = Math.Min(bosh, r.Length); oxir = Math.Min(oxir, r.Length);
        if (bosh != oxir) { Ornat(t, r[..bosh] + r[oxir..], bosh); return; }
        var i = Math.Min(RaqamIndeksi(matn, t.CaretIndex), r.Length);
        if (e.Key == Key.Back && i > 0) Ornat(t, r[..(i - 1)] + r[i..], i - 1);
        else if (e.Key == Key.Delete && i < r.Length) Ornat(t, r[..i] + r[(i + 1)..], i);
        else Ornat(t, r, i);
    }

    /// <summary>Joylashtirish / dasturdan o'rnatish: istalgan ko'rinish "+998 XX XXX XX XX" ga keltiriladi, kursor oxirga.</summary>
    private static void Normallashtir(TextBox t)
    {
        if (t.GetValue(IshlanmoqdaProperty)) return;
        var eski = t.Text ?? "";
        var yangi = Format.TelefonKiritish(Format.TelefonRaqamlari(eski));
        if (yangi == eski) return;
        t.SetValue(IshlanmoqdaProperty, true);
        try { t.Text = yangi; }
        finally { t.SetValue(IshlanmoqdaProperty, false); }
        t.CaretIndex = yangi.Length;
        Dispatcher.UIThread.Post(() => { if (t.Text == yangi) t.CaretIndex = yangi.Length; }, DispatcherPriority.Input);
    }
}
