using Avalonia;
using Avalonia.Controls;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// Bog'langan qatordan klass qo'yish: ctl:Klass.Nomi="{Binding BelgiKlassi}" ("yashil", "qizil"...).
/// Eski qiymatning klassi olib tashlanadi. Bir nechta klass bo'sh joy bilan.
/// </summary>
public static class Klass
{
    public static readonly AttachedProperty<string?> NomiProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Nomi", typeof(Klass));

    public static string? GetNomi(Control c) => c.GetValue(NomiProperty);
    public static void SetNomi(Control c, string? v) => c.SetValue(NomiProperty, v);

    static Klass()
    {
        NomiProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            if (e.OldValue is string eski)
                foreach (var k in eski.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)) c.Classes.Remove(k);
            if (e.NewValue is string yangi)
                foreach (var k in yangi.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)) c.Classes.Add(k);
        });
    }
}
