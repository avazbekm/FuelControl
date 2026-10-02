using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// Chiziqli (stroke) ikonka. 24×24 koordinatada chiziladi, Olcham bo'yicha masshtablanadi.
/// Foydalanish: &lt;ctl:Ikon Nomi="grid" Olcham="18" Rang="White"/&gt;
/// </summary>
public sealed class Ikon : Viewbox
{
    public static readonly StyledProperty<string> NomiProperty =
        AvaloniaProperty.Register<Ikon, string>(nameof(Nomi), "grid");

    public static readonly StyledProperty<IBrush> RangProperty =
        AvaloniaProperty.Register<Ikon, IBrush>(nameof(Rang), new SolidColorBrush(Color.Parse("#2E3D5C")));

    public static readonly StyledProperty<double> OlchamProperty =
        AvaloniaProperty.Register<Ikon, double>(nameof(Olcham), 18);

    public static readonly StyledProperty<double> QalinlikProperty =
        AvaloniaProperty.Register<Ikon, double>(nameof(Qalinlik), 2);

    public string Nomi { get => GetValue(NomiProperty); set => SetValue(NomiProperty, value); }
    public IBrush Rang { get => GetValue(RangProperty); set => SetValue(RangProperty, value); }
    public double Olcham { get => GetValue(OlchamProperty); set => SetValue(OlchamProperty, value); }
    public double Qalinlik { get => GetValue(QalinlikProperty); set => SetValue(QalinlikProperty, value); }

    private readonly Path _path;

    public Ikon()
    {
        _path = new Path
        {
            Width = 24, Height = 24, Stretch = Stretch.None,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Fill = null,
        };
        Child = _path;
        Stretch = Stretch.Uniform;
        Yangila();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NomiProperty || change.Property == RangProperty ||
            change.Property == OlchamProperty || change.Property == QalinlikProperty)
            Yangila();
    }

    private void Yangila()
    {
        Width = Olcham; Height = Olcham;
        _path.Stroke = Rang;
        _path.StrokeThickness = Qalinlik;
        _path.Data = Geometry.Parse(Yollar.TryGetValue(Nomi, out var d) ? d : Yollar["grid"]);
    }

    private static readonly Dictionary<string, string> Yollar = new()
    {
        ["grid"] = "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
        ["plus"] = "M12 5v14 M5 12h14",
        ["clock"] = "M21 12A9 9 0 1 1 3 12A9 9 0 1 1 21 12 M12 7v5l3 2",
        ["file"] = "M6 3h8l4 4v14H6z M9 13h6 M9 17h6",
        ["users"] = "M12.5 8A3.5 3.5 0 1 1 5.5 8A3.5 3.5 0 1 1 12.5 8 M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6 M19.5 9A2.5 2.5 0 1 1 14.5 9A2.5 2.5 0 1 1 19.5 9 M21 19c0-2.5-1.8-4.5-4-4.8",
        ["list"] = "M4 6h16 M4 12h16 M4 18h16",
        ["gear"] = "M15 12A3 3 0 1 1 9 12A3 3 0 1 1 15 12 M12 2v3 M12 19v3 M2 12h3 M19 12h3 M4.9 4.9l2.1 2.1 M17 17l2.1 2.1 M4.9 19.1L7 17 M17 7l2.1-2.1",
        ["power"] = "M12 3v9 M6.3 6.3a8 8 0 1 0 11.4 0",
        ["drop"] = "M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z",
        ["download"] = "M12 4v11 M7 10l5 5 5-5 M4 19h16",
        ["refresh"] = "M20 12a8 8 0 1 1-2.3-5.7 M20 4v5h-5",
        ["cash"] = "M4 6h16a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M15 12A3 3 0 1 1 9 12A3 3 0 1 1 15 12",
        ["card"] = "M4 5h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z M2 10h20",
        ["phone"] = "M9 2h6a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z M11 18h2",
        ["mix"] = "M4 8h13l-3-3 M20 16H7l3 3",
        ["check"] = "M5 12l5 5L20 7",
        ["excel"] = "M6 3h8l4 4v14H6z M9 11l6 6 M15 11l-6 6",
        ["arrow"] = "M5 12h14 M13 6l6 6-6 6",
        ["search"] = "M18 11A7 7 0 1 1 4 11A7 7 0 1 1 18 11 M20 20l-4-4",
        ["pump"] = "M5 21V5a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v16 M3 21h14 M15 9h2a2 2 0 0 1 2 2v6a1.5 1.5 0 0 0 3 0V9l-2-2 M8 6h4v4H8z",
        ["up"] = "M7 14l5-5 5 5",
        ["down"] = "M7 10l5 5 5-5",
        ["back"] = "M19 12H5 M11 6l-6 6 6 6",
        ["backspace"] = "M21 6H8l-5 6 5 6h13a1 1 0 0 0 1-1V7a1 1 0 0 0-1-1z M12 9l6 6 M18 9l-6 6",
        ["save"] = "M5 3h11l3 3v15H5z M8 3v6h8V3 M8 21v-7h8v7",
        ["wrench"] = "M14.7 6.3a4 4 0 0 0 5 5L13 18a2 2 0 0 1-3 3l-7-7a2 2 0 0 1 3-3l6.7-6.7z",
        ["user"] = "M16 8A4 4 0 1 1 8 8A4 4 0 1 1 16 8 M4 21c0-4.4 3.6-8 8-8s8 3.6 8 8",
        ["folder"] = "M3 7h6l2 2h10v10H3z",
        ["pin"] = "M12 2a7 7 0 0 1 7 7c0 5-7 13-7 13S5 14 5 9a7 7 0 0 1 7-7z M12 9h.01",
        ["edit"] = "M12 20h9 M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z",
        ["sun"] = "M17 12A5 5 0 1 1 7 12A5 5 0 1 1 17 12 M12 1v2 M12 21v2 M4.2 4.2l1.4 1.4 M18.4 18.4l1.4 1.4 M1 12h2 M21 12h2 M4.2 19.8l1.4-1.4 M18.4 5.6l1.4-1.4",
        ["moon"] = "M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z",
        ["panelLeft"] = "M3 5h18v14H3z M9 5v14",
        ["chevronsLeft"] = "M11 17l-5-5 5-5 M18 17l-5-5 5-5",
        ["chevronsRight"] = "M13 17l5-5-5-5 M6 17l5-5-5-5",
        ["shield"] = "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6l8-3z M9 12l2 2 4-4",
        ["play"] = "M6 4l14 8-14 8z",
        ["stop"] = "M5 5h14v14H5z",
        ["lock"] = "M6 11h12v10H6z M8 11V7a4 4 0 0 1 8 0v4",
    };
}
