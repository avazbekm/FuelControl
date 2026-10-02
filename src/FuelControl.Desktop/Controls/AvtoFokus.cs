using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// ctl:AvtoFokus.Yoqilgan="True" — element (dialog ichidagi TextBox) ko'ringan zahoti fokus oladi.
/// Dialog pardasi (ajdod) IsVisible=true bo'lganda ishga tushadi.
/// </summary>
public static class AvtoFokus
{
    public static readonly AttachedProperty<bool> YoqilganProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("Yoqilgan", typeof(AvtoFokus));

    public static bool GetYoqilgan(Control c) => c.GetValue(YoqilganProperty);
    public static void SetYoqilgan(Control c, bool v) => c.SetValue(YoqilganProperty, v);

    static AvtoFokus()
    {
        YoqilganProperty.Changed.AddClassHandler<Control>((c, e) =>
        {
            if (e.NewValue is not true) return;
            c.AttachedToVisualTree += (_, _) =>
            {
                Fokusla(c);
                // Barcha ajdodlarning ko'rinishini kuzatish (dialog pardasi ochilganda)
                foreach (var ajdod in c.GetVisualAncestors())
                {
                    if (ajdod is not Visual v) continue;
                    v.PropertyChanged += (_, a) =>
                    {
                        if (a.Property == Visual.IsVisibleProperty && a.NewValue is true) Fokusla(c);
                    };
                }
            };
        });
    }

    private static void Fokusla(Control c) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!c.IsEffectivelyVisible) return;
            c.Focus();
            if (c is TextBox t) t.SelectAll();
        }, DispatcherPriority.Input);
}
