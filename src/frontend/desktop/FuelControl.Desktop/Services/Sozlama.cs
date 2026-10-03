using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FuelControl.Desktop.Services;

/// <summary>Shu kompyuterda oxirgi kirgan foydalanuvchi — login ekranida tez tanlash va operatorga PIN klaviatura uchun.</summary>
public sealed record OxirgiKirgan(string Login, string ToliqIsm, Rol Rol);

/// <summary>
/// %AppData%\FuelControl\sozlamalar.json — server manzili va oxirgi kirganlar.
/// Foydalanuvchi fayli hali yo'q bo'lsa, o'rnatuvchi dastur papkasiga yozgan sozlama.json dagi manzil standart bo'ladi.
/// </summary>
public sealed class Sozlama
{
    public const string StandartManzil = "http://localhost:5000";

    public string ServerManzili { get; set; } = StandartManzil;
    public List<OxirgiKirgan> OxirgiKirganlar { get; set; } = new();

    private static readonly JsonSerializerOptions Js = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static string Fayl => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FuelControl", "sozlamalar.json");

    /// <summary>O'rnatuvchi yozadigan standart: {dastur papkasi}\sozlama.json → {"ServerManzili": "http://localhost:5000"}.</summary>
    private static string DasturFayli => Path.Combine(AppContext.BaseDirectory, "sozlama.json");

    public static Sozlama Joriy { get; } = Oqi(Fayl, DasturFayli);

    /// <summary>Foydalanuvchi fayli o'qilsa — o'sha; aks holda dastur papkasidagi standart manzil; ikkalasi ham bo'lmasa — StandartManzil.</summary>
    public static Sozlama Oqi(string foydalanuvchiFayli, string dasturFayli)
    {
        try
        {
            if (File.Exists(foydalanuvchiFayli))
                return JsonSerializer.Deserialize<Sozlama>(File.ReadAllText(foydalanuvchiFayli), Js) ?? new();
        }
        catch (Exception) { /* buzilgan fayl — standart manzilga o'tamiz */ }

        try
        {
            if (File.Exists(dasturFayli)
                && JsonSerializer.Deserialize<Sozlama>(File.ReadAllText(dasturFayli), Js)?.ServerManzili is { } manzil
                && Uri.TryCreate(manzil.Trim(), UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
                return new Sozlama { ServerManzili = manzil.Trim().TrimEnd('/') };
        }
        catch (Exception) { /* buzilgan standart fayl — e'tiborsiz */ }
        return new();
    }

    public void Saqla()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Fayl)!);
            File.WriteAllText(Fayl, JsonSerializer.Serialize(this, Js));
        }
        catch (Exception) { /* yozib bo'lmasa — ish davom etadi */ }
    }

    public void KirganniEslab(OxirgiKirgan k)
    {
        OxirgiKirganlar.RemoveAll(x => x.Login.Equals(k.Login, StringComparison.OrdinalIgnoreCase));
        OxirgiKirganlar.Insert(0, k);
        OxirgiKirganlar = OxirgiKirganlar.Take(8).ToList();
        Saqla();
    }
}
