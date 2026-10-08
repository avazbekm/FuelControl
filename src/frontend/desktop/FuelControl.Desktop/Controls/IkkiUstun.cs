using System;
using Avalonia;
using Avalonia.Controls;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// Asosiy + yon panel (birinchi bola — asosiy, ikkinchisi — yon, eni <see cref="YonEni"/>).
/// Asosiy qismga <see cref="AsosiyMinEni"/> dan kam joy qolsa, yon panel pastga tushadi (to'liq enda) —
/// jadval siqilib, raqamlar o'qib bo'lmas darajada kichraymaydi.
/// </summary>
public class IkkiUstun : Panel
{
    public static readonly StyledProperty<double> YonEniProperty = AvaloniaProperty.Register<IkkiUstun, double>(nameof(YonEni), 300);
    public static readonly StyledProperty<double> AsosiyMinEniProperty = AvaloniaProperty.Register<IkkiUstun, double>(nameof(AsosiyMinEni), 600);
    public static readonly StyledProperty<double> OraliqProperty = AvaloniaProperty.Register<IkkiUstun, double>(nameof(Oraliq), 18);

    static IkkiUstun() => AffectsMeasure<IkkiUstun>(YonEniProperty, AsosiyMinEniProperty, OraliqProperty);

    public double YonEni { get => GetValue(YonEniProperty); set => SetValue(YonEniProperty, value); }
    public double AsosiyMinEni { get => GetValue(AsosiyMinEniProperty); set => SetValue(AsosiyMinEniProperty, value); }
    public double Oraliq { get => GetValue(OraliqProperty); set => SetValue(OraliqProperty, value); }

    private Control? Asosiy => Children.Count > 0 ? Children[0] : null;
    private Control? Yon => Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;
    private bool Yonma(double en) => double.IsInfinity(en) || en >= AsosiyMinEni + Oraliq + YonEni;

    protected override Size MeasureOverride(Size available)
    {
        var en = available.Width;
        if (Yon is null) { Asosiy?.Measure(new Size(en, double.PositiveInfinity)); return Asosiy?.DesiredSize ?? default; }
        if (Yonma(en))
        {
            var asosiyEn = double.IsInfinity(en) ? double.PositiveInfinity : en - Oraliq - YonEni;
            Asosiy?.Measure(new Size(asosiyEn, double.PositiveInfinity));
            Yon.Measure(new Size(YonEni, double.PositiveInfinity));
            var a = Asosiy?.DesiredSize ?? default;
            return new Size(double.IsInfinity(en) ? a.Width + Oraliq + YonEni : en, Math.Max(a.Height, Yon.DesiredSize.Height));
        }
        Asosiy?.Measure(new Size(en, double.PositiveInfinity));
        Yon.Measure(new Size(en, double.PositiveInfinity));
        return new Size(en, (Asosiy?.DesiredSize.Height ?? 0) + Oraliq + Yon.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (Yon is null) { Asosiy?.Arrange(new Rect(0, 0, final.Width, Asosiy.DesiredSize.Height)); return final; }
        if (Yonma(final.Width))
        {
            var asosiyEn = final.Width - Oraliq - YonEni;
            Asosiy?.Arrange(new Rect(0, 0, asosiyEn, Asosiy.DesiredSize.Height));
            Yon.Arrange(new Rect(asosiyEn + Oraliq, 0, YonEni, Yon.DesiredSize.Height));
        }
        else
        {
            var h = Asosiy?.DesiredSize.Height ?? 0;
            Asosiy?.Arrange(new Rect(0, 0, final.Width, h));
            Yon.Arrange(new Rect(0, h + Oraliq, final.Width, Yon.DesiredSize.Height));
        }
        return final;
    }
}
