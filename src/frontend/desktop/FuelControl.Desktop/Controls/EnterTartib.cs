using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// Enter bilan keyingi maydonga o'tish (§8.4): ctl:EnterTartib.Guruh="yopish" berilgan elementlar oynadagi ko'rinish tartibida navbat bo'ladi.
/// TextBox'da Enter bosilsa — fokus guruhdagi keyingi ko'rinadigan va faol elementga o'tadi (tugma bo'lsa faqat fokus oladi, bosilmaydi).
/// Enter shu yerda "ishlatilgan" bo'ladi, shuning uchun IsDefault tugma ham o'zi bosilmaydi.
/// TextBox'dan tashqari kiritish elementlari (masalan, CalendarDatePicker) ham navbatda qatnashadi; tugmaga ishlov
/// biriktirilmaydi — fokusdagi tugmada Enter odatdagidek uni bosadi. Ko'p qatorli maydonda Shift+Enter — yangi qator.
/// </summary>
public static class EnterTartib
{
    public static readonly AttachedProperty<string?> GuruhProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Guruh", typeof(EnterTartib));

    public static string? GetGuruh(Control c) => c.GetValue(GuruhProperty);
    public static void SetGuruh(Control c, string? v) => c.SetValue(GuruhProperty, v);

    static EnterTartib()
    {
        GuruhProperty.Changed.AddClassHandler<Control>((t, e) =>
        {
            if (t is Button) return;
            t.RemoveHandler(InputElement.KeyDownEvent, KeyBosildi);
            if (e.NewValue is string { Length: > 0 }) t.AddHandler(InputElement.KeyDownEvent, KeyBosildi, RoutingStrategies.Tunnel);
        });
    }

    private static void KeyBosildi(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return) || e.KeyModifiers != KeyModifiers.None || sender is not Control c) return;
        var guruh = GetGuruh(c);
        if (TopLevel.GetTopLevel(c) is not { } top || guruh is null) return;
        // Vizual daraxt tartibi = sahifadagi joylashuv tartibi (chap ustun yuqoridan pastga, keyin o'ng panel).
        var navbat = top.GetVisualDescendants().OfType<Control>()
            .Where(x => GetGuruh(x) == guruh && (x == c || (x.IsEffectivelyVisible && x.IsEffectivelyEnabled && x.Focusable)))
            .ToList();
        var i = navbat.IndexOf(c);
        e.Handled = true;
        if (i < 0 || i + 1 >= navbat.Count) return;
        var keyingi = navbat[i + 1];
        keyingi.Focus(NavigationMethod.Tab);
        if (keyingi is TextBox t) t.SelectAll();
    }
}
