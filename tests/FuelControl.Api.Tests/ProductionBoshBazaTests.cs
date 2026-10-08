using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>
/// Production (mijoz o'rnatuvchisi): bo'sh bazaga faqat admin yoziladi - yoqilg'i, aparat, smena, nasiya yo'q (Seed:DemoMalumot=true
/// yozilib qolsa ham). Yoqilg'i va aparatlarni admin Sozlamalar'da o'zi kiritadi, shundan keyingina smena ochiladi.
/// </summary>
public sealed class ProductionBoshBazaTests : ApiBaza
{
    private const decimal Pult1 = 184_642.30m, Pult2 = 98_765.40m;

    protected override string Muhit => "Production";
    protected override bool DemoSozlamasi => true;

    [Fact]
    public async Task BoshBaza_FaqatAdmin_YoqilgiAparatSmenaNasiyaVaDemoYoq()
    {
        var admin = await Admin();

        var men = (await admin.GetFromJsonAsync<FoydalanuvchiDto>("/me", Json))!;
        Assert.Equal(("admin", Rol.Admin), (men.Login, men.Rol));
        Assert.Equal(["admin"], (await admin.GetFromJsonAsync<FoydalanuvchiDto[]>("/foydalanuvchilar", Json))!.Select(f => f.Login));
        Assert.Empty((await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!);
        Assert.Empty((await admin.GetFromJsonAsync<NarxTarixiDto[]>("/yoqilgilar/narx-tarixi", Json))!);
        Assert.Empty(await Aparatlar(admin));
        Assert.Empty((await admin.GetFromJsonAsync<SmenaDto[]>("/smenalar", Json))!);
        var nasiyalar = (await admin.GetFromJsonAsync<NasiyalarDto>("/nasiyalar", Json))!;
        Assert.Empty(nasiyalar.Royxat);
        Assert.Equal((0L, 0), (nasiyalar.Xulosa.FaolQarz, nasiyalar.Xulosa.FaolSoni));
        Assert.Empty((await admin.GetFromJsonAsync<MijozTaklifDto[]>("/nasiyalar/mijozlar", Json))!);
        await Kut(HttpStatusCode.NoContent, await admin.GetAsync("/smenalar/joriy"));
        await Kut(HttpStatusCode.NoContent, await admin.GetAsync("/smenalar/oxirgi"));
    }

    [Fact]
    public async Task BoshBaza_ParametrsizGetEndpointlarHammasiMuvaffaqiyatliJavobBeradi()
    {
        var admin = await Admin();
        using var openapi = JsonDocument.Parse(await admin.GetStringAsync("/openapi/v1.json"));
        var yollar = openapi.RootElement.GetProperty("paths").EnumerateObject()
            .Where(p => p.Value.TryGetProperty("get", out _) && !p.Name.Contains('{')).Select(p => p.Name).ToList();

        Assert.True(yollar.Count >= 15, "OpenAPI'da kutilgan GET endpointlar topilmadi: " + string.Join(", ", yollar));
        foreach (var yol in yollar)
        {
            var javob = await admin.GetAsync(yol);
            Assert.True(javob.IsSuccessStatusCode, $"GET {yol} -> {(int)javob.StatusCode}: {await javob.Content.ReadAsStringAsync()}");
        }
    }

    [Fact]
    public async Task SmenaOchish_AparatYoq_400_KamidaBittaAparatKerak()
    {
        var admin = await Admin();
        var (_, op) = await Yarat("ali");

        foreach (var mijoz in new[] { admin, op })
        {
            var javob = await mijoz.PostAsJsonAsync("/smenalar/och", new SmenaOchishDto(100_000, 50_000, 200_000), Json);
            await Kut(HttpStatusCode.BadRequest, javob);
            Assert.Equal("Smenani ochish uchun kamida bitta aparat kerak. Sozlamalar → Aparatlar bo'limida aparat qo'shing.", await Detail(javob));
        }
        await Kut(HttpStatusCode.NoContent, await admin.GetAsync("/smenalar/joriy"));            // smena ochilmadi
    }

    [Fact]
    public async Task Oqim_YoqilgiYaratish_AparatYaratish_SmenaOchish_Yopish_OldingiBoshlangichKorsatkich()
    {
        var admin = await Admin();
        var (_, op) = await Yarat("ali");

        var ai92 = await Oqi<YoqilgiTuriDto>(await admin.PostAsJsonAsync("/yoqilgilar", new YoqilgiYaratishDto("AI-92", 12_200, "#2563EB"), Json));
        var dizel = await Oqi<YoqilgiTuriDto>(await admin.PostAsJsonAsync("/yoqilgilar", new YoqilgiYaratishDto("Dizel", 13_800, "#CA8A04"), Json));
        var a1 = await Oqi<AparatDto>(await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(1, ai92.Id, Pult1, 5_000m), Json));
        var a2 = await Oqi<AparatDto>(await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(2, dizel.Id, Pult2, 8_000m), Json));
        Assert.Equal((Pult1, 5_000m, Pult2, 8_000m), (a1.TotalLitr, a1.BakQoldiq, a2.TotalLitr, a2.BakQoldiq));

        var smena = await Och(op);                                   // aparat bor - endi smena ochiladi

        // Pult ko'rsatkichi boshlang'ich (oldingi) qiymatdan kichik bo'lsa - 400, smena ochiq qoladi.
        await Kut(HttpStatusCode.BadRequest, await Yop(op, smena.Id, [new(a1.Id, Pult1 - 1m), new(a2.Id, Pult2 + 50.50m)]));
        Assert.Null((await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Smena.Tugadi);

        var yopilgan = await Oqi<SmenaDto>(await Yop(op, smena.Id, [new(a1.Id, Pult1 + 100m), new(a2.Id, Pult2 + 50.50m)], naqd: 2_016_900));
        // Savdo = 100.00 x 12 200 + 50.50 x 13 800; kutilgan naqd = qaytim 100 000 + savdo (terminal va depozit o'zgarmagan).
        Assert.Equal((150.50m, 1_916_900L, 2_016_900L, 0L), (yopilgan.JamiLitr, yopilgan.Savdo, yopilgan.Kutilgan, yopilgan.Farq));

        var segmentlar = (await Oqi<SmenaTafsilotDto>(await op.GetAsync($"/smenalar/{smena.Id}"))).Korsatkichlar;
        var s1 = segmentlar.Single(k => k.AparatId == a1.Id);
        var s2 = segmentlar.Single(k => k.AparatId == a2.Id);
        // Oldingi = aparat yaratilgandagi boshlang'ich ko'rsatkich.
        Assert.Equal((Pult1, Pult1 + 100m, 100.00m, 1_220_000L), (s1.Boshi, s1.Oxiri, s1.Litr, s1.Summa));
        Assert.Equal((Pult2, Pult2 + 50.50m, 50.50m, 696_900L), (s2.Boshi, s2.Oxiri, s2.Litr, s2.Summa));

        var aparatlar = await Aparatlar(op);                        // TotalLitr yangi ko'rsatkichga, bak sotilgan litrga kamaydi
        var y1 = aparatlar.Single(a => a.Id == a1.Id);
        var y2 = aparatlar.Single(a => a.Id == a2.Id);
        Assert.Equal((Pult1 + 100m, 4_900m, Pult2 + 50.50m, 7_949.50m), (y1.TotalLitr, y1.BakQoldiq, y2.TotalLitr, y2.BakQoldiq));
    }
}
