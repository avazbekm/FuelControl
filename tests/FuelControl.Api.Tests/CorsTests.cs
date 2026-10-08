using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Cors:Manbalar konfiguratsiyasi: faqat ro'yxatdagi aniq manbalarga ruxsat; ro'yxat bo'sh bo'lsa CORS umuman yoqilmaydi.</summary>
public sealed class CorsTests : IAsyncLifetime
{
    private const string Ruxsatli = "https://fuelcontrol-web.pages.dev";
    private const string Boshqa = "https://boshqa-sayt.example";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly string _papka = Path.Combine(Path.GetTempPath(), "fc-test-" + Guid.NewGuid().ToString("N"));
    private readonly List<WebApplicationFactory<Program>> _ilovalar = [];

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_papka);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        foreach (var i in _ilovalar) await i.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_papka, true); } catch (IOException) { }
    }

    private HttpClient Mijoz(params string[] manbalar)
    {
        var ilova = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:Baza", $"Data Source={Path.Combine(_papka, Guid.NewGuid().ToString("N") + ".db")}");
            b.UseSetting("Jwt:Kalit", "test-kalit-0123456789abcdef0123456789abcdef");
            b.UseSetting("Seed:AdminParol", "admin1234");
            b.UseSetting("Zaxira:Papka", Path.Combine(_papka, "zaxira"));
            for (var i = 0; i < manbalar.Length; i++) b.UseSetting($"Cors:Manbalar:{i}", manbalar[i]);
        });
        _ilovalar.Add(ilova);
        return ilova.CreateClient();
    }

    private static HttpRequestMessage Sorov(HttpMethod usul, string yol, string? origin)
    {
        var s = new HttpRequestMessage(usul, yol);
        if (origin is not null) s.Headers.TryAddWithoutValidation("Origin", origin);
        return s;
    }

    private static HttpRequestMessage Preflight(string yol, string origin, string metod = "POST", string sarlavhalar = "authorization,content-type,x-signalr-user-agent")
    {
        var s = Sorov(HttpMethod.Options, yol, origin);
        s.Headers.TryAddWithoutValidation("Access-Control-Request-Method", metod);
        s.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", sarlavhalar);
        return s;
    }

    private static string? Sarlavha(HttpResponseMessage j, string nom) =>
        j.Headers.TryGetValues(nom, out var v) ? string.Join(",", v) : null;

    [Fact]
    public async Task RuxsatEtilganManba_JavobdaCorsSarlavhalariBor()
    {
        var mijoz = Mijoz(Ruxsatli);
        var j = await mijoz.SendAsync(Sorov(HttpMethod.Get, "/openapi/v1.json", Ruxsatli));
        Assert.Equal(HttpStatusCode.OK, j.StatusCode);
        Assert.Equal(Ruxsatli, Sarlavha(j, "Access-Control-Allow-Origin"));
        Assert.Equal("true", Sarlavha(j, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task BoshqaManba_CorsSarlavhasiYoq_VaPreflightRadEtiladi()
    {
        var mijoz = Mijoz(Ruxsatli);
        var oddiy = await mijoz.SendAsync(Sorov(HttpMethod.Get, "/openapi/v1.json", Boshqa));
        Assert.Null(Sarlavha(oddiy, "Access-Control-Allow-Origin"));

        var pre = await mijoz.SendAsync(Preflight("/nasiyalar", Boshqa));
        Assert.Null(Sarlavha(pre, "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("/nasiyalar", "POST")]
    [InlineData("/auth/login", "POST")]
    [InlineData("/smenalar/5", "GET")]
    [InlineData("/yoqilgilar/3", "DELETE")]
    [InlineData("/aparatlar/2", "PUT")]
    [InlineData("/hub/negotiate", "POST")]
    public async Task Preflight_204_ManbaMetodVaSarlavhalarBilan(string yol, string metod)
    {
        var mijoz = Mijoz(Ruxsatli);
        var j = await mijoz.SendAsync(Preflight(yol, Ruxsatli, metod));
        Assert.Equal(HttpStatusCode.NoContent, j.StatusCode);
        Assert.Equal(Ruxsatli, Sarlavha(j, "Access-Control-Allow-Origin"));
        Assert.Contains(metod, Sarlavha(j, "Access-Control-Allow-Methods")!);
        var sarlavhalar = Sarlavha(j, "Access-Control-Allow-Headers")!.ToLowerInvariant();
        Assert.Contains("authorization", sarlavhalar);
        Assert.Contains("content-type", sarlavhalar);
        Assert.Contains("x-signalr-user-agent", sarlavhalar);
        Assert.Equal("3600", Sarlavha(j, "Access-Control-Max-Age"));
    }

    [Fact]
    public async Task BiznesXatosiJavobidaHamCorsSarlavhasiBor()
    {
        // 400/404/409 — istisnodan UseExceptionHandler yasaydi; brauzer javobni ko'rishi uchun CORS sarlavhasi shunda ham kerak.
        var mijoz = Mijoz(Ruxsatli);
        var kirish = await mijoz.PostAsJsonAsync("/auth/login", new LoginSoroviDto("admin", "admin1234"), Json);
        var token = (await kirish.Content.ReadFromJsonAsync<LoginJavobiDto>(Json))!.Token;

        var s = Sorov(HttpMethod.Get, "/hisobot?guruh=xato", Ruxsatli);
        s.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var j = await mijoz.SendAsync(s);
        Assert.Equal(HttpStatusCode.BadRequest, j.StatusCode);
        Assert.Equal(Ruxsatli, Sarlavha(j, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ManbalarBerilmasa_CorsYoqilmaydi()
    {
        var mijoz = Mijoz();
        var oddiy = await mijoz.SendAsync(Sorov(HttpMethod.Get, "/openapi/v1.json", Ruxsatli));
        Assert.Equal(HttpStatusCode.OK, oddiy.StatusCode);
        Assert.Null(Sarlavha(oddiy, "Access-Control-Allow-Origin"));

        var pre = await mijoz.SendAsync(Preflight("/nasiyalar", Ruxsatli));
        Assert.Null(Sarlavha(pre, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ManbaYozuvi_BoshJoylarVaOxirgiSlashEtiborgaOlinmaydi()
    {
        var mijoz = Mijoz("  " + Ruxsatli + "/  ", "   ");
        var j = await mijoz.SendAsync(Sorov(HttpMethod.Get, "/openapi/v1.json", Ruxsatli));
        Assert.Equal(Ruxsatli, Sarlavha(j, "Access-Control-Allow-Origin"));
    }
}
