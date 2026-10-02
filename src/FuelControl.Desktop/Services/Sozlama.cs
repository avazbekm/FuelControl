using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FuelControl.Desktop.Services;

/// <summary>Shu kompyuterda oxirgi kirgan foydalanuvchi — login ekranida tez tanlash va operatorga PIN klaviatura uchun.</summary>
public sealed record OxirgiKirgan(string Login, string ToliqIsm, Rol Rol);

/// <summary>%AppData%\FuelControl\sozlamalar.json — server manzili va oxirgi kirganlar.</summary>
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

    public static Sozlama Joriy { get; } = Oqi();

    private static Sozlama Oqi()
    {
        try
        {
            if (File.Exists(Fayl)) return JsonSerializer.Deserialize<Sozlama>(File.ReadAllText(Fayl), Js) ?? new();
        }
        catch (Exception) { /* buzilgan fayl — standart sozlama */ }
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
