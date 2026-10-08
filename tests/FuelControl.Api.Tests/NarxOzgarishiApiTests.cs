using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Ochiq smenada narx o'zgarishi: pult ko'rsatkichi majburiy, eski narxda alohida segment, qolgani yopishda yangi narxda.</summary>
public sealed class NarxOzgarishiApiTests : ApiBaza
{
    private static async Task<YoqilgiTuriDto> Ai92(HttpClient m) => (await m.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!.Single(y => y.Nomi == "AI-92");

    private static Task<HttpResponseMessage> Narx(HttpClient m, YoqilgiTuriDto y, long narx, params AparatKorsatkichDto[] korsatkichlar) =>
        m.PutAsJsonAsync($"/yoqilgilar/{y.Id}", new YoqilgiTahrirlashDto(y.Nomi, narx, y.Rang, korsatkichlar.Length == 0 ? null : korsatkichlar), Json);

    private static async Task<int[]> KerakliAparatlar(HttpResponseMessage javob) =>
        JsonDocument.Parse(await javob.Content.ReadAsStringAsync()).RootElement.GetProperty("kerakliAparatlar").EnumerateArray().Select(e => e.GetInt32()).ToArray();

    [Fact]
    public async Task OchiqSmenada_KorsatkichSiz400_KorsatkichBilanIkkiSegment_YopishdaYangiNarx()
    {
        var (_, op) = await Yarat("ali");
        var admin = await Admin();
        var yoqilgi = await Ai92(admin);
        var smena = await Och(op);
        var aparatlar = await Aparatlar(admin);                                  // 1- va 2-aparat AI-92 (12 200)
        var (a1, a2) = (aparatlar[0], aparatlar[1]);

        // Ko'rsatkichsiz yoki yetarlimas - 400, ProblemDetails.extensions.kerakliAparatlar: shu yoqilg'ining hamma aparatlari / yetishmaganlari.
        var yoq = await Narx(admin, yoqilgi, 13_000);
        await Kut(HttpStatusCode.BadRequest, yoq);
        Assert.Equal([a1.Id, a2.Id], await KerakliAparatlar(yoq));
        var yarim = await Narx(admin, yoqilgi, 13_000, new AparatKorsatkichDto(a1.Id, 100m));
        await Kut(HttpStatusCode.BadRequest, yarim);
        Assert.Equal([a2.Id], await KerakliAparatlar(yarim));
        await Kut(HttpStatusCode.BadRequest, await Narx(admin, yoqilgi, 13_000, new AparatKorsatkichDto(a1.Id, -1m), new AparatKorsatkichDto(a2.Id, 5m)));   // oldingidan kichik
        await Kut(HttpStatusCode.BadRequest, await Narx(admin, yoqilgi, 13_000, new AparatKorsatkichDto(a1.Id, 5m), new AparatKorsatkichDto(a2.Id, 5m),
            new AparatKorsatkichDto(aparatlar[2].Id, 1m)));                                                                                                 // boshqa yoqilg'i aparati
        Assert.Equal(12_200, (await Ai92(admin)).Narx);                           // hech narsa o'zgarmadi

        var o = await Oqi<YoqilgiTuriDto>(await Narx(admin, yoqilgi, 13_000, new AparatKorsatkichDto(a1.Id, 100m), new AparatKorsatkichDto(a2.Id, 50m)));
        Assert.Equal(13_000, o.Narx);

        // Ochiq smenada faqat narx o'zgarishida qayd etilgan (eski narxdagi) segmentlar; savdo hali 0.
        var jonli = await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"));
        Assert.Equal(0, jonli.Smena.Savdo);
        Assert.Equal([(a1.Id, 0m, 100m, 12_200L, 100m, 1_220_000L, true), (a2.Id, 0m, 50m, 12_200L, 50m, 610_000L, true)],
            jonli.Korsatkichlar.Select(k => (k.AparatId, k.Boshi, k.Oxiri, k.Narx, k.Litr, k.Summa, k.NarxOzgarishida)));
        var hozirgi = (await Aparatlar(admin))[0];
        Assert.Equal((0m, 0m), (hozirgi.TotalLitr, hozirgi.BakQoldiq));            // pult va bak faqat yopilganda o'zgaradi

        // Yopish: oxirgi segment yangi narxda (13 000). 1-aparat 250 (150 L), 2-aparat 80 (30 L), qolganlari 0.
        var yopildi = await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 250m, 80m)));
        Assert.Equal((4_170_000L, 330m), (yopildi.Savdo, yopildi.JamiLitr));               // 1 220 000 + 610 000 + 1 950 000 + 390 000
        var tafsilot = await Oqi<SmenaTafsilotDto>(await admin.GetAsync($"/smenalar/{smena.Id}"));
        Assert.Equal(7, tafsilot.Korsatkichlar.Length);                                     // 1-, 2-aparat: 2 tadan, qolgan uchtasi: 1 tadan
        Assert.Equal([(1, 0m, 100m, 12_200L, true), (1, 100m, 250m, 13_000L, false), (2, 0m, 50m, 12_200L, true), (2, 50m, 80m, 13_000L, false)],
            tafsilot.Korsatkichlar.Take(4).Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx, k.NarxOzgarishida)));
        Assert.Equal([250m, 80m], (await Aparatlar(admin)).Take(2).Select(a => a.TotalLitr));
        Assert.Equal([-250m, -80m], (await Aparatlar(admin)).Take(2).Select(a => a.BakQoldiq));
    }

    [Fact]
    public async Task IkkinchiMartaOzgarish_Zanjir_VaOddiyHolatlar()
    {
        var (_, op) = await Yarat("ali");
        var admin = await Admin();
        var yoqilgi = await Ai92(admin);
        var aparatlar = await Aparatlar(admin);
        var (a1, a2) = (aparatlar[0], aparatlar[1]);

        // Ochiq smena yo'q: ko'rsatkich kerak emas, narx shunchaki o'zgaradi (narx tarixiga yoziladi).
        await Oqi<YoqilgiTuriDto>(await Narx(admin, yoqilgi, 12_500));
        Assert.Equal(12_500, (await Ai92(admin)).Narx);
        Assert.Single((await admin.GetFromJsonAsync<NarxTarixiDto[]>("/yoqilgilar/narx-tarixi", Json))!);

        var smena = await Och(op);
        // Narx o'zgarmasa (faqat rang) - ko'rsatkich kerak emas.
        await Oqi<YoqilgiTuriDto>(await admin.PutAsJsonAsync($"/yoqilgilar/{yoqilgi.Id}", new YoqilgiTahrirlashDto("AI-92", 12_500, "#112233"), Json));

        await Oqi<YoqilgiTuriDto>(await Narx(admin, await Ai92(admin), 13_000, new AparatKorsatkichDto(a1.Id, 100m), new AparatKorsatkichDto(a2.Id, 100m)));
        // 2-aparat ko'rsatkichi o'zgarmagan (hech narsa sotilmagan) - segment yozilmaydi; 1-aparat 120 gacha eski (13 000) narxda segment.
        await Oqi<YoqilgiTuriDto>(await Narx(admin, await Ai92(admin), 14_000, new AparatKorsatkichDto(a1.Id, 120m), new AparatKorsatkichDto(a2.Id, 100m)));
        var jonli = await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"));
        Assert.Equal([(1, 0m, 100m, 12_500L), (1, 100m, 120m, 13_000L), (2, 0m, 100m, 12_500L)],
            jonli.Korsatkichlar.Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx)));

        // Yopish: oxirgi segment oxirgi narxda (14 000): 1-aparat 150 (30 L), 2-aparat 100 (0 L).
        var yopildi = await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 150m, 100m)));
        Assert.Equal(100 * 12_500 + 100 * 12_500 + 20 * 13_000 + 30 * 14_000, yopildi.Savdo);

        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?q=Narx%20o%27zgardi", Json))!;
        Assert.Contains(audit, a => a.Tafsilot == "AI-92: 12 500 dan 13 000 ga · ochiq smenada 2 ta aparat ko'rsatkichi qayd etildi" && a.Tur == "sozlama");
        Assert.Contains(audit, a => a.Tafsilot == "AI-92: 13 000 dan 14 000 ga · ochiq smenada 1 ta aparat ko'rsatkichi qayd etildi");
    }

    [Fact]
    public async Task Ruxsat_NomTakrori_YoqilgiTopilmadi()
    {
        var (_, op) = await Yarat("ali");
        var admin = await Admin();
        var yoqilgilar = (await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!;
        await Kut(HttpStatusCode.Forbidden, await Narx(op, yoqilgilar[0], 20_000));                                    // Sozlamalar ruxsati yo'q
        var takror = await admin.PutAsJsonAsync($"/yoqilgilar/{yoqilgilar[0].Id}", new YoqilgiTahrirlashDto(yoqilgilar[1].Nomi, 12_200, "#000000"), Json);
        await Kut(HttpStatusCode.Conflict, takror);
        await Kut(HttpStatusCode.NotFound, await Narx(admin, yoqilgilar[0] with { Id = 9999 }, 20_000));
    }
}
