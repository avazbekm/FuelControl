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

/// <summary>
/// Aparat yaratish/tahrirlash — to'liq API (vaqtinchalik SQLite fayl) bo'yicha haqiqiy so'rov-javob aylanishi:
/// web va desktop dialoglari shu endpoint'lar bilan ishlaydi.
/// </summary>
public sealed class AparatTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly string _papka = Path.Combine(Path.GetTempPath(), "fc-test-" + Guid.NewGuid().ToString("N"));
    private WebApplicationFactory<Program> _ilova = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_papka);
        _ilova = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Testing");
            b.UseSetting("ConnectionStrings:Baza", $"Data Source={Path.Combine(_papka, "fc.db")}");
            b.UseSetting("Jwt:Kalit", "test-kalit-0123456789abcdef0123456789abcdef");
            b.UseSetting("Seed:AdminParol", "admin1234");
            b.UseSetting("Zaxira:Papka", Path.Combine(_papka, "zaxira"));
        });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _ilova.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_papka, true); } catch (IOException) { }
    }

    private async Task<HttpClient> AdminKir()
    {
        var mijoz = _ilova.CreateClient();
        var javob = await mijoz.PostAsJsonAsync("/auth/login", new LoginSoroviDto("admin", "admin1234"), Json);
        javob.EnsureSuccessStatusCode();
        var token = (await javob.Content.ReadFromJsonAsync<LoginJavobiDto>(Json))!.Token;
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return mijoz;
    }

    private static async Task<YoqilgiTuriDto[]> Yoqilgilar(HttpClient admin) =>
        (await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!;

    private static async Task<AparatDto> Aparatlar(HttpClient admin, int id) =>
        (await admin.GetFromJsonAsync<AparatDto[]>("/aparatlar", Json))!.Single(a => a.Id == id);

    private static async Task<AuditYozuviDto[]> Audit(HttpClient admin, string q) =>
        (await admin.GetFromJsonAsync<AuditYozuviDto[]>($"/audit?q={Uri.EscapeDataString(q)}&limit=100", Json))!;

    private static async Task<string?> Detail(HttpResponseMessage javob) =>
        (await javob.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("detail").GetString();

    [Fact]
    public async Task Yaratish_BoshlangichTotalizatorGetdaKorinadi_AuditgaYoziladi()
    {
        var admin = await AdminKir();
        var yoqilgi = (await Yoqilgilar(admin))[0];

        // Seed 1–5 raqamli aparatlarni yaratgan; 6 bo'sh.
        var javob = await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(6, yoqilgi.Id, 1234.5m), Json);
        Assert.Equal(HttpStatusCode.Created, javob.StatusCode);
        var yaratilgan = (await javob.Content.ReadFromJsonAsync<AparatDto>(Json))!;
        Assert.Equal(1234.5m, yaratilgan.TotalLitr);

        var royxatda = await Aparatlar(admin, yaratilgan.Id);
        Assert.Equal(6, royxatda.Raqam);
        Assert.Equal(yoqilgi.Id, royxatda.YoqilgiTuriId);
        Assert.Equal(yoqilgi.Nomi, royxatda.YoqilgiNomi);
        Assert.Equal(1234.5m, royxatda.TotalLitr);

        Assert.Contains(await Audit(admin, "Aparat yaratildi"), a => a.Amal == "Aparat yaratildi" && a.Tafsilot.Contains("#6"));
    }

    [Fact]
    public async Task Tahrirlash_TotalizatorFaqatBerilsaVaFarqQilsaOzgaradi_AuditgaTuzatildiYoziladi()
    {
        var admin = await AdminKir();
        var yoqilgilar = await Yoqilgilar(admin);
        var yaratilgan = (await (await admin.PostAsJsonAsync("/aparatlar",
            new AparatYaratishDto(6, yoqilgilar[0].Id, 1234.5m), Json)).Content.ReadFromJsonAsync<AparatDto>(Json))!;
        var yol = $"/aparatlar/{yaratilgan.Id}";

        // 1) TotalLitr berilmasa: raqam va yoqilg'i o'zgaradi, totalizator o'zgarmaydi, "Totalizator tuzatildi" yozilmaydi.
        var j1 = await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(7, yoqilgilar[1].Id), Json);
        Assert.Equal(HttpStatusCode.OK, j1.StatusCode);
        var d1 = (await j1.Content.ReadFromJsonAsync<AparatDto>(Json))!;
        Assert.Equal((7, yoqilgilar[1].Id, 1234.5m), (d1.Raqam, d1.YoqilgiTuriId, d1.TotalLitr));
        Assert.Equal(1234.5m, (await Aparatlar(admin, yaratilgan.Id)).TotalLitr);
        Assert.Contains(await Audit(admin, "Aparat o'zgartirildi"), a => a.Amal == "Aparat o'zgartirildi");
        Assert.Empty(await Audit(admin, "Totalizator tuzatildi"));

        // 2) Bir xil qiymat berilsa — o'zgarish ham, audit ham yo'q.
        var j2 = await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(7, yoqilgilar[1].Id, 1234.5m), Json);
        Assert.Equal(HttpStatusCode.OK, j2.StatusCode);
        Assert.Equal(1234.5m, (await Aparatlar(admin, yaratilgan.Id)).TotalLitr);
        Assert.Empty(await Audit(admin, "Totalizator tuzatildi"));

        // 3) Boshqa qiymat berilsa — o'zgaradi (javobda ham, GET'da ham) va auditga yoziladi.
        var j3 = await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(7, yoqilgilar[1].Id, 2000.25m), Json);
        Assert.Equal(HttpStatusCode.OK, j3.StatusCode);
        Assert.Equal(2000.25m, (await j3.Content.ReadFromJsonAsync<AparatDto>(Json))!.TotalLitr);
        Assert.Equal(2000.25m, (await Aparatlar(admin, yaratilgan.Id)).TotalLitr);
        var tuzatish = Assert.Single(await Audit(admin, "Totalizator tuzatildi"));
        Assert.Equal("Totalizator tuzatildi", tuzatish.Amal);
        Assert.Contains("#7", tuzatish.Tafsilot);

        // 4) Manfiy qiymat rad etiladi, totalizator o'zgarmaydi.
        var j4 = await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(7, yoqilgilar[1].Id, -1m), Json);
        Assert.Equal(HttpStatusCode.BadRequest, j4.StatusCode);
        Assert.Equal(2000.25m, (await Aparatlar(admin, yaratilgan.Id)).TotalLitr);
    }

    [Fact]
    public async Task Xatolar_StatusVaOzbekchaXabarBilanQaytadi()
    {
        var admin = await AdminKir();
        var yoqilgi = (await Yoqilgilar(admin))[0];

        var band = await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(1, yoqilgi.Id, 0m), Json);
        Assert.Equal(HttpStatusCode.Conflict, band.StatusCode);
        Assert.Equal("Bunday raqamli aparat bor.", await Detail(band));

        var manfiy = await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(6, yoqilgi.Id, -5m), Json);
        Assert.Equal(HttpStatusCode.BadRequest, manfiy.StatusCode);
        Assert.Equal("Totalizator manfiy bo'lmasligi kerak.", await Detail(manfiy));

        var yoqilgiYoq = await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(6, 9999, 0m), Json);
        Assert.Equal(HttpStatusCode.NotFound, yoqilgiYoq.StatusCode);
        Assert.Equal("Yoqilg'i topilmadi.", await Detail(yoqilgiYoq));

        var aparatYoq = await admin.PutAsJsonAsync("/aparatlar/9999", new AparatTahrirlashDto(8, yoqilgi.Id), Json);
        Assert.Equal(HttpStatusCode.NotFound, aparatYoq.StatusCode);
        Assert.Equal("Aparat topilmadi.", await Detail(aparatYoq));

        // Xatolardan birortasi ham aparat yaratmagan: seeddagi 5 ta qolgan.
        Assert.Equal(5, (await admin.GetFromJsonAsync<AparatDto[]>("/aparatlar", Json))!.Length);
    }
}
