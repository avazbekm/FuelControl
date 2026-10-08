using System;
using Avalonia;
using Avalonia.Controls;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// Moslashuvchan to'r: ustunlar soni eniga qarab tanlanadi — har element kamida <see cref="ElementEni"/> px oladi,
/// ustunlar <see cref="MaksUstun"/> dan oshmaydi. Joy yetmasa ustun kamayadi (kesilish o'rniga yangi qator).
/// UniformGrid'dan farqi: qatorlar balandligi o'z qatoridagi eng baland element bo'yicha.
/// </summary>
public class Moslashuvchan : Panel
{
    public static readonly StyledProperty<double> ElementEniProperty = AvaloniaProperty.Register<Moslashuvchan, double>(nameof(ElementEni), 220);
    public static readonly StyledProperty<int> MaksUstunProperty = AvaloniaProperty.Register<Moslashuvchan, int>(nameof(MaksUstun), 4);
    public static readonly StyledProperty<double> ColumnSpacingProperty = AvaloniaProperty.Register<Moslashuvchan, double>(nameof(ColumnSpacing), 12);
    public static readonly StyledProperty<double> RowSpacingProperty = AvaloniaProperty.Register<Moslashuvchan, double>(nameof(RowSpacing), 12);

    static Moslashuvchan() => AffectsMeasure<Moslashuvchan>(ElementEniProperty, MaksUstunProperty, ColumnSpacingProperty, RowSpacingProperty);

    /// <summary>Bitta element uchun eng kam en (px).</summary>
    public double ElementEni { get => GetValue(ElementEniProperty); set => SetValue(ElementEniProperty, value); }
    public int MaksUstun { get => GetValue(MaksUstunProperty); set => SetValue(MaksUstunProperty, value); }
    public double ColumnSpacing { get => GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }
    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    private int Ustunlar(double en)
    {
        var n = 0;
        foreach (var c in Children) if (c.IsVisible) n++;
        var maks = Math.Max(1, Math.Min(MaksUstun, Math.Max(n, 1)));
        if (double.IsInfinity(en)) return maks;
        var sig = (int)Math.Floor((en + ColumnSpacing) / (ElementEni + ColumnSpacing));
        return Math.Clamp(sig, 1, maks);
    }

    private double ElementEniHisobla(double en, int ustun) =>
        double.IsInfinity(en) ? ElementEni : Math.Max(0, (en - ColumnSpacing * (ustun - 1)) / ustun);

    protected override Size MeasureOverride(Size available)
    {
        var ustun = Ustunlar(available.Width);
        var en = ElementEniHisobla(available.Width, ustun);
        double balandlik = 0, qator = 0; int i = 0, qatorlar = 0;
        foreach (var c in Children)
        {
            if (!c.IsVisible) continue;
            c.Measure(new Size(en, double.PositiveInfinity));
            qator = Math.Max(qator, c.DesiredSize.Height);
            if (++i % ustun == 0) { balandlik += qator; qator = 0; qatorlar++; }
        }
        if (i % ustun != 0) { balandlik += qator; qatorlar++; }
        balandlik += Math.Max(0, qatorlar - 1) * RowSpacing;
        var jamiEn = double.IsInfinity(available.Width) ? ustun * en + (ustun - 1) * ColumnSpacing : available.Width;
        return new Size(i == 0 ? 0 : jamiEn, balandlik);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var ustun = Ustunlar(final.Width);
        var en = ElementEniHisobla(final.Width, ustun);
        var korinadi = new System.Collections.Generic.List<Control>();
        foreach (var c in Children) if (c.IsVisible) korinadi.Add(c);
        double y = 0;
        for (int bosh = 0; bosh < korinadi.Count; bosh += ustun)
        {
            double qator = 0;
            for (int j = bosh; j < Math.Min(bosh + ustun, korinadi.Count); j++) qator = Math.Max(qator, korinadi[j].DesiredSize.Height);
            for (int j = bosh; j < Math.Min(bosh + ustun, korinadi.Count); j++)
                korinadi[j].Arrange(new Rect((j - bosh) * (en + ColumnSpacing), y, en, qator));
            y += qator + RowSpacing;
        }
        return final;
    }
}
