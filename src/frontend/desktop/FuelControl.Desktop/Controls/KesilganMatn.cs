using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// "…" bilan qisqartiriladigan har bir TextBlock (TextTrimming != None) avtomatik ToolTip oladi — to'liq matn.
/// ToolTip faqat matn haqiqatan kesilganda ochiladi. Alohida ToolTip berilgan joyga tegilmaydi.
/// </summary>
public static class KesilganMatn
{
    private static bool _yoqilgan;

    public static void Yoqish()
    {
        if (_yoqilgan) return;
        _yoqilgan = true;
        TextBlock.TextTrimmingProperty.Changed.AddClassHandler<TextBlock>((tb, e) =>
        {
            if (e.NewValue is TextTrimming t && t != TextTrimming.None && !tb.IsSet(ToolTip.TipProperty))
                tb[!ToolTip.TipProperty] = tb[!TextBlock.TextProperty];
        });
        ToolTip.ToolTipOpeningEvent.AddClassHandler<TextBlock>((tb, e) =>
        {
            if (tb.TextTrimming != TextTrimming.None && !tb.TextLayout.TextLines.Any(l => l.HasCollapsed)
                && ReferenceEquals(ToolTip.GetTip(tb), tb.Text))
                e.Cancel = true;
        }, RoutingStrategies.Direct | RoutingStrategies.Bubble);
    }
}
