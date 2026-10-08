using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>To'liq API (vaqtinchalik SQLite fayl). Token eski bo'lsa ham server foydalanuvchining joriy holatini tekshiradi.</summary>
public sealed class JoriyRuxsatTests : IAsyncLifetime
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

    private async Task<HttpClient> Kir(string login, string parol)
    {
        var mijoz = _ilova.CreateClient();
        var javob = await mijoz.PostAsJsonAsync("/auth/login", new LoginSoroviDto(login, parol), Json);
        javob.EnsureSuccessStatusCode();
        var token = (await javob.Content.ReadFromJsonAsync<LoginJavobiDto>(Json))!.Token;
        mijoz.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return mijoz;
    }

    [Fact]
    public async Task OlibTashlanganRuxsat403_YangiRuxsatQaytaKirishsizIshlaydi_Nofaol401()
    {
        var admin = await Kir("admin", "admin1234");
        var yaratildi = await admin.PostAsJsonAsync("/foydalanuvchilar",
            new FoydalanuvchiYaratishDto("Sardor", "sardor", Rol.Operator, 4_000_000, "1111"), Json);
        var op = (await yaratildi.Content.ReadFromJsonAsync<FoydalanuvchiDto>(Json))!;

        var operatorMijoz = await Kir("sardor", "1111");
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorMijoz.GetAsync("/audit")).StatusCode);

        // Ruxsat berildi — o'sha token bilan darhol ishlaydi.
        await admin.PutAsJsonAsync($"/foydalanuvchilar/{op.Id}/ruxsatlar",
            new RuxsatlarOrnatishDto([Ruxsat.Savdo, Ruxsat.SmenaOchish, Ruxsat.SmenaYopish, Ruxsat.Audit]), Json);
        Assert.Equal(HttpStatusCode.OK, (await operatorMijoz.GetAsync("/audit")).StatusCode);

        // SmenaOchish olib tashlandi — token'da bor bo'lsa ham 403.
        await admin.PutAsJsonAsync($"/foydalanuvchilar/{op.Id}/ruxsatlar", new RuxsatlarOrnatishDto([Ruxsat.Savdo]), Json);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorMijoz.PostAsJsonAsync("/smenalar/och", new SmenaOchishDto(0, 0, 0), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operatorMijoz.GetAsync("/audit")).StatusCode);

        // Nofaol qilindi — har qanday so'rov 401.
        await admin.PutAsJsonAsync($"/foydalanuvchilar/{op.Id}", new FoydalanuvchiTahrirlashDto("Sardor", Rol.Operator, false, 4_000_000), Json);
        Assert.Equal(HttpStatusCode.Unauthorized, (await operatorMijoz.GetAsync("/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await operatorMijoz.GetAsync("/smenalar/joriy")).StatusCode);

        // Admin ishlashda davom etadi.
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/me")).StatusCode);
    }

    [Fact]
    public async Task RuxsatYokiFaollikOzgarsa_SignalRUlanishiUziladi_IsmOzgarsaUzilmaydi()
    {
        var admin = await Kir("admin", "admin1234");
        var op = (await (await admin.PostAsJsonAsync("/foydalanuvchilar",
            new FoydalanuvchiYaratishDto("Hub Op", "hubop", Rol.Operator, 0, "1111"), Json)).Content.ReadFromJsonAsync<FoydalanuvchiDto>(Json))!;
        var token = (await Kir("hubop", "1111")).DefaultRequestHeaders.Authorization!.Parameter!;
        var ulanishlar = _ilova.Services.GetRequiredService<FuelControl.Api.Xizmatlar.UlanishlarXaritasi>();

        async Task<(HubConnection Hub, TaskCompletionSource QaytaUlan, TaskCompletionSource Yopildi)> Ulan()
        {
            var server = _ilova.Server;
            var hub = new HubConnectionBuilder()
                .WithUrl(new Uri(server.BaseAddress, "hub"), o =>
                {
                    o.HttpMessageHandlerFactory = _ => server.CreateHandler();
                    o.Transports = HttpTransportType.LongPolling;
                    o.AccessTokenProvider = () => Task.FromResult<string?>(token);
                })
                .Build();
            var qaytaUlan = new TaskCompletionSource();
            var yopildi = new TaskCompletionSource();
            hub.On("QaytaUlan", () => qaytaUlan.TrySetResult());
            hub.Closed += _ => { yopildi.TrySetResult(); return Task.CompletedTask; };
            await hub.StartAsync();
            for (int i = 0; i < 50 && ulanishlar.Soni(op.Id) == 0; i++) await Task.Delay(100);
            Assert.Equal(1, ulanishlar.Soni(op.Id));
            return (hub, qaytaUlan, yopildi);
        }

        // Faqat ism o'zgarsa — ulanish saqlanadi.
        var (hub1, qayta1, yopildi1) = await Ulan();
        await admin.PutAsJsonAsync($"/foydalanuvchilar/{op.Id}", new FoydalanuvchiTahrirlashDto("Hub Operator", Rol.Operator, true, 0), Json);
        await Task.Delay(500);
        Assert.False(qayta1.Task.IsCompleted);
        Assert.Equal(HubConnectionState.Connected, hub1.State);

        // Ruxsat o'zgarsa — "QaytaUlan" keladi va server ulanishni uzadi.
        await admin.PutAsJsonAsync($"/foydalanuvchilar/{op.Id}/ruxsatlar", new RuxsatlarOrnatishDto([Ruxsat.Savdo, Ruxsat.Smenalar]), Json);
        await qayta1.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await yopildi1.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, ulanishlar.Soni(op.Id));

        // Qayta ulansa (yangi ruxsat bilan) — ishlaydi; nofaol qilinsa yana uziladi va qayta ulana olmaydi.
        var (hub2, qayta2, yopildi2) = await Ulan();
        await admin.PutAsJsonAsync($"/foydalanuvchilar/{op.Id}", new FoydalanuvchiTahrirlashDto("Hub Operator", Rol.Operator, false, 0), Json);
        await qayta2.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await yopildi2.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAnyAsync<Exception>(Ulan);

        await hub1.DisposeAsync();
        await hub2.DisposeAsync();
    }

    [Fact]
    public async Task OpenApi_JavobTurlariBor_SonlarFaqatSon()
    {
        var mijoz = _ilova.CreateClient();
        var sxema = JsonDocument.Parse(await mijoz.GetStringAsync("/openapi/v1.json")).RootElement;
        string Javob(string yol, string usul, string kod) =>
            sxema.GetProperty("paths").GetProperty(yol).GetProperty(usul).GetProperty("responses").GetProperty(kod)
                .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!;
        Assert.EndsWith("/LoginJavobiDto", Javob("/auth/login", "post", "200"));
        Assert.EndsWith("/FoydalanuvchiDto", Javob("/me", "get", "200"));
        Assert.EndsWith("/OperatorHisobDto", Javob("/operatorlar/{id}/hisob", "get", "200"));
        Assert.EndsWith("/SmenaTafsilotDto", Javob("/smenalar/{id}", "get", "200"));

        var summa = sxema.GetProperty("components").GetProperty("schemas").GetProperty("SmenaDto").GetProperty("properties").GetProperty("savdo");
        Assert.Equal("integer", summa.GetProperty("type").GetString());
        Assert.False(summa.TryGetProperty("pattern", out _));
    }

    [Fact]
    public async Task PwaStatikFayllari_ManifestMime()
    {
        var mijoz = _ilova.CreateClient();
        var wwwroot = _ilova.Services.GetRequiredService<IWebHostEnvironment>().WebRootPath;
        if (wwwroot is null || !File.Exists(Path.Combine(wwwroot, "index.html"))) return; // web hali build qilinmagan
        var bosh = await mijoz.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, bosh.StatusCode);
        Assert.Equal("text/html", bosh.Content.Headers.ContentType!.MediaType);
        var manifest = await mijoz.GetAsync("/manifest.webmanifest");
        Assert.Equal("application/manifest+json", manifest.Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData("operator", HttpStatusCode.OK)]
    [InlineData("smena", HttpStatusCode.OK)]
    [InlineData("KUN", HttpStatusCode.OK)]
    [InlineData("Oy", HttpStatusCode.OK)]
    [InlineData(null, HttpStatusCode.OK)]
    [InlineData("xato", HttpStatusCode.BadRequest)]
    [InlineData("7", HttpStatusCode.BadRequest)]
    [InlineData("1", HttpStatusCode.BadRequest)]
    public async Task HisobotGuruhi_HarfgaBoglanmaydi_NotogriQiymat400(string? guruh, HttpStatusCode kutilgan)
    {
        var admin = await Kir("admin", "admin1234");
        var javob = await admin.GetAsync("/hisobot" + (guruh is null ? "" : $"?guruh={guruh}"));
        Assert.Equal(kutilgan, javob.StatusCode);
        if (kutilgan == HttpStatusCode.BadRequest)
            Assert.Equal("application/problem+json", javob.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task NotogriParametrTuri_400ProblemDetails()
    {
        var admin = await Kir("admin", "admin1234");
        var javob = await admin.GetAsync("/hisobot?dan=bugun");
        Assert.Equal(HttpStatusCode.BadRequest, javob.StatusCode);
        Assert.Equal("application/problem+json", javob.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task EksportAuditgaYoziladi_RuxsatsizRadEtiladi()
    {
        var admin = await Kir("admin", "admin1234");
        var javob = await admin.PostAsJsonAsync("/audit/eksport", new AuditEksportDto("Hisobot", "2026-10-01 — 2026-10-02"), Json);
        Assert.Equal(HttpStatusCode.NoContent, javob.StatusCode);
        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?limit=5", Json))!;
        Assert.Contains(audit, a => a.Amal == "Eksport: Hisobot");

        await admin.PostAsJsonAsync("/foydalanuvchilar", new FoydalanuvchiYaratishDto("Jasur", "jasur", Rol.Operator, 0, "2222"), Json);
        var op = await Kir("jasur", "2222");
        Assert.Equal(HttpStatusCode.Forbidden, (await op.PostAsJsonAsync("/audit/eksport", new AuditEksportDto("Hisobot", ""), Json)).StatusCode);
    }
}
