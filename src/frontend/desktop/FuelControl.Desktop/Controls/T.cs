using System;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.Controls;

/// <summary>
/// XAML tarjima: Text="{l:T Kirish}". Til.Joriy.Kod ga bog'lanadi — til almashganda konverter qayta hisoblaydi.
/// </summary>
public sealed class T : MarkupExtension
{
    public string Kalit { get; set; }
    /// <summary>Katta harf bilan (yorliqlar uchun).</summary>
    public bool Katta { get; set; }
    /// <summary>Oldiga qo'shiladigan matn (masalan "● ").</summary>
    public string Oldi { get; set; } = "";
    /// <summary>Keyiniga qo'shiladigan matn (masalan ":").</summary>
    public string Keyin { get; set; } = "";

    public T(string kalit) => Kalit = kalit;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var kalit = Kalit; var katta = Katta; var oldi = Oldi; var keyin = Keyin;
        return new Binding(nameof(Til.Kod))
        {
            Source = Til.Joriy,
            Mode = BindingMode.OneWay,
            Converter = new FuncValueConverter<string?, string>(_ =>
            {
                var m = Til.T(kalit);
                return oldi + (katta ? m.ToUpperInvariant() : m) + keyin;
            }),
        };
    }
}
