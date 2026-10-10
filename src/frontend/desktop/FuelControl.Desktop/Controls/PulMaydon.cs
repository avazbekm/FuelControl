using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// Pul maydoni: ctl:PulMaydon.Yoqilgan="True" — maydondan chiqilganda (fokus yo'qolganda, jumladan Enter bilan keyingisiga o'tganda)
/// to'g'ri yozilgan summa ajratgich bilan formatlanadi: "7830000" → "7 830 000". Yozish paytida (kursor, kiritish) aralashmaydi;
/// bo'sh, manfiy yoki raqamdan boshqa belgili yozuvga tegilmaydi (xato o'z holicha ko'rinadi). Qiymat (Format.PulOl) o'zgarmaydi.
/// </summary>
public static class PulMaydon
{
    public static readonly AttachedProperty<bool> YoqilganProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Yoqilgan", typeof(PulMaydon));

    public static bool GetYoqilgan(TextBox t) => t.GetValue(YoqilganProperty);
    public static void SetYoqilgan(TextBox t, bool v) => t.SetValue(YoqilganProperty, v);

    static PulMaydon()
    {
        YoqilganProperty.Changed.AddClassHandler<TextBox>((t, e) =>
        {
            t.RemoveHandler(InputElement.LostFocusEvent, Chiqdi);
            if (e.NewValue is true) t.AddHandler(InputElement.LostFocusEvent, Chiqdi, RoutingStrategies.Bubble);
        });
    }

    private static void Chiqdi(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox t && Formatla(t.Text) is { } f && f != t.Text) t.Text = f;
    }

    /// <summary>To'g'ri summa bo'lsa — formatlangan matn, aks holda null (o'zgartirilmaydi).</summary>
    public static string? Formatla(string? matn)
    {
        var x = (matn ?? "").Trim();
        if (x.Length == 0 || !x.All(c => char.IsDigit(c) || c == ' ' || c == ' ')) return null;
        return Format.PulOl(x) is { } v ? Format.Pul(v) : null;
    }
}
