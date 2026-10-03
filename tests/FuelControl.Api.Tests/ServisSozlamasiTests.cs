using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>
/// Windows xizmati sifatida ishlatish uchun sozlamalar: fayl jurnali (Log:Papka), ishga tushmaganda sabab jurnalda qolishi
/// va administrator tahrirlaydigan appsettings.Local.json (o'rnatuvchi unga tegmaydi).
/// </summary>
public sealed class ServisSozlamasiTests : IAsyncLifetime
{
    private readonly string _papka = Path.Combine(Path.GetTempPath(), "fc-test-" + Guid.NewGuid().ToString("N"));
    private readonly List<WebApplicationFactory<Program>> _ilovalar = [];

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_papka);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var i in _ilovalar)
        {
            try { await i.DisposeAsync(); } catch (Exception) { /* ishga tushmagan host'ni yopish */ }
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_papka, true); } catch (IOException) { }
    }

    private WebApplicationFactory<Program> Ilova(Action<IWebHostBuilder> sozla, bool adminParol = true)
    {
        var ilova = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:Baza", $"Data Source={Path.Combine(_papka, Guid.NewGuid().ToString("N") + ".db")}");
            b.UseSetting("Jwt:Kalit", "test-kalit-0123456789abcdef0123456789abcdef");
            if (adminParol) b.UseSetting("Seed:AdminParol", "admin1234");
            b.UseSetting("Zaxira:Papka", Path.Combine(_papka, "zaxira"));
            sozla(b);
        });
        _ilovalar.Add(ilova);
        return ilova;
    }

    /// <summary>Jurnal fayli ochiq turganda ham o'qiladi (Serilog shared: true).</summary>
    private static string JurnalMatni(string papka)
    {
        var fayllar = Directory.Exists(papka) ? Directory.GetFiles(papka, "fuelcontrol-*.log") : [];
        Assert.NotEmpty(fayllar);
        using var oqim = new FileStream(fayllar[0], FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var oquvchi = new StreamReader(oqim);
        return oquvchi.ReadToEnd();
    }

    [Fact]
    public void JurnalPapkasiBerilsa_KunlikFaylgaVersiyaBilanYoziladi()
    {
        var logPapka = Path.Combine(_papka, "logs");
        Ilova(b => b.UseSetting("Log:Papka", logPapka)).CreateClient();

        var matn = JurnalMatni(logPapka);
        Assert.Matches(new Regex(@"\[INF\] .*FuelControl API tayyor\. Versiya \d+\.\d+\.\d+, muhit Testing"), matn);
        Assert.Matches(@"fuelcontrol-\d{8}\.log$", Directory.GetFiles(logPapka, "fuelcontrol-*.log")[0]);
    }

    [Fact]
    public void JurnalPapkasiBerilmasa_FaylYaratilmaydi()
    {
        var logPapka = Path.Combine(_papka, "logs-yoq");
        Ilova(_ => { }).CreateClient();
        Assert.False(Directory.Exists(logPapka));
    }

    [Fact]
    public void AdminParoliYoqBoshBaza_IshgaTushmaydi_SababiJurnaldaQoladi()
    {
        var logPapka = Path.Combine(_papka, "logs-xato");
        var ilova = Ilova(b => b.UseSetting("Log:Papka", logPapka), adminParol: false);

        Assert.ThrowsAny<Exception>(() => ilova.CreateClient());

        var matn = JurnalMatni(logPapka);
        Assert.Contains("[FTL]", matn);
        Assert.Contains("FuelControl API ishga tushmadi", matn);
        Assert.Contains("Seed__AdminParol", matn);
    }

    [Fact]
    public async Task LocalJson_KonfiguratsiyaniBelgilaydi()
    {
        const string origin = "https://local-json.example";
        var ildiz = Path.Combine(_papka, "ildiz");
        Directory.CreateDirectory(ildiz);
        await File.WriteAllTextAsync(Path.Combine(ildiz, "appsettings.Local.json"),
            $$"""{ "Cors": { "Manbalar": [ "{{origin}}" ] } }""");

        var mijoz = Ilova(b => b.UseContentRoot(ildiz)).CreateClient();
        var s = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        s.Headers.TryAddWithoutValidation("Origin", origin);
        var j = await mijoz.SendAsync(s);

        Assert.True(j.IsSuccessStatusCode);
        Assert.Equal(origin, Assert.Single(j.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
