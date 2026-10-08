using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Oxirgi yopilgan smenada pult ko'rsatkichini tuzatish (sotuv tahririning o'rni): qayta hisob, kamomat/ortiqcha harakati, aparat/bak, audit.</summary>
public sealed class KorsatkichTuzatishApiTests : ApiBaza
{
    private static Task<HttpResponseMessage> Tuzat(HttpClient m, int smenaId, int aparatId, decimal qiymat, string sabab = "Pult noto'g'ri o'qilgan") =>
        m.PutAsJsonAsync($"/smenalar/{smenaId}/korsatkich", new KorsatkichTuzatishDto(aparatId, qiymat, sabab), Json);

    /// <summary>1-aparat 100 L (12 200 so'm) sotilgan, farq 0 bilan yopilgan smena: kutilgan = 100 000 + 1 220 000.</summary>
    private async Task<SmenaDto> YopilganSmena(HttpClient op, AparatDto[] aparatlar)
    {
        var smena = await Och(op);
        return await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 100m), terminal: 50_000, depozit: 200_000, naqd: 1_320_000));
    }

    [Fact]
    public async Task Tuzatish_QaytaHisob_FarqHarakatlari_AparatBakVaAudit()
    {
        var (ali, op) = await Yarat("ali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var admin = await Admin();
        var aparatlar = await Aparatlar(op);
        var yopilgan = await YopilganSmena(op, aparatlar);
        Assert.Equal(0, yopilgan.Farq);
        Assert.DoesNotContain((await Oqi<OperatorHisobDto>(await admin.GetAsync($"/operatorlar/{ali.Id}/hisob"))).Harakatlar, h => h.Turi == HarakatTuri.Kamomat);

        // 100.00 -> 110.50: savdo +128 100 (10.5 L x 12 200), kutilgan oshdi, farq -128 100: qo'shimcha kamomat.
        var t1 = await Oqi<SmenaTafsilotDto>(await Tuzat(bosh, yopilgan.Id, aparatlar[0].Id, 110.5m));
        Assert.Equal((1_348_100L, 110.5m, 1_448_100L, -128_100L), (t1.Smena.Savdo, t1.Smena.JamiLitr, t1.Smena.Kutilgan, t1.Smena.Farq));
        Assert.Equal((0m, 110.5m, 1_348_100L), (t1.Korsatkichlar[0].Boshi, t1.Korsatkichlar[0].Oxiri, t1.Korsatkichlar[0].Summa));
        var a1 = (await Aparatlar(op))[0];
        Assert.Equal((110.5m, -110.5m), (a1.TotalLitr, a1.BakQoldiq));                         // delta bo'yicha: pult +10.5, bak -10.5

        // 110.50 -> 90.00: savdo kamaydi, farq +122 000: avvalgi kamomat -128 100 dan +122 000 ga - ortiqcha harakati +250 100.
        var t2 = await Oqi<SmenaTafsilotDto>(await Tuzat(bosh, yopilgan.Id, aparatlar[0].Id, 90m, "Qayta tekshirildi"));
        Assert.Equal((1_098_000L, 122_000L), (t2.Smena.Savdo, t2.Smena.Farq));

        var hisob = await Oqi<OperatorHisobDto>(await admin.GetAsync($"/operatorlar/{ali.Id}/hisob"));
        var tuzatishlar = hisob.Harakatlar.Where(h => h.Izoh.Contains("tuzatish")).OrderBy(h => h.Id).ToList();
        Assert.Equal(2, tuzatishlar.Count);
        Assert.Equal((HarakatTuri.Kamomat, -128_100L, "boshliq"), (tuzatishlar[0].Turi, tuzatishlar[0].Summa, tuzatishlar[0].KimYozdi));
        Assert.Equal($"Smena #{yopilgan.Id} tuzatish: 1-aparat 100.00 dan 110.50 ga. Sabab: Pult noto'g'ri o'qilgan", tuzatishlar[0].Izoh);
        Assert.Equal((HarakatTuri.Ortiqcha, 250_100L), (tuzatishlar[1].Turi, tuzatishlar[1].Summa));
        Assert.Equal(122_000L, tuzatishlar.Sum(h => h.Summa));                                  // jami ta'sir = yangi farq (+122 000) - eski farq (0)

        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=tuzatish", Json))!;
        Assert.Contains(audit, a => a.Amal == "Ko'rsatkich tuzatildi" && a.Kim == "boshliq"
            && a.Tafsilot == $"Smena #{yopilgan.Id} · 1-aparat yangi ko'rsatkich 100.00 dan 110.50 ga · sabab: Pult noto'g'ri o'qilgan");
    }

    [Fact]
    public async Task Tuzatish_Qoidalari_RuxsatSababOxirgiSmenaVaOchiqSmena()
    {
        var (_, op) = await Yarat("ali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var aparatlar = await Aparatlar(op);
        var birinchi = await YopilganSmena(op, aparatlar);
        var a1 = aparatlar[0].Id;

        await Kut(HttpStatusCode.Forbidden, await Tuzat(op, birinchi.Id, a1, 105m));                        // operatorda KorsatkichTuzatish yo'q
        await Kut(HttpStatusCode.BadRequest, await Tuzat(bosh, birinchi.Id, a1, 105m, "  "));              // sabab majburiy
        await Kut(HttpStatusCode.BadRequest, await Tuzat(bosh, birinchi.Id, a1, 100m));                     // qiymat o'zgarmagan
        await Kut(HttpStatusCode.BadRequest, await Tuzat(bosh, birinchi.Id, a1, -1m));                      // smena boshidagi ko'rsatkichdan kichik
        await Kut(HttpStatusCode.NotFound, await Tuzat(bosh, birinchi.Id, 9999, 105m));
        await Kut(HttpStatusCode.NotFound, await Tuzat(bosh, 9999, a1, 105m));

        // Ochiq smena: avval yopish kerak. Faqat OXIRGI yopilgan smena tuzatiladi.
        var ikkinchi = await Och(op);
        await Kut(HttpStatusCode.Conflict, await Tuzat(bosh, ikkinchi.Id, a1, 105m));
        var yopiq = await Oqi<SmenaDto>(await Yop(op, ikkinchi.Id, Oxirgi(await Aparatlar(op), 5m), terminal: 50_000, depozit: 200_000, naqd: 0));
        var eski = await Tuzat(bosh, birinchi.Id, a1, 105m);
        await Kut(HttpStatusCode.Conflict, eski);
        Assert.Contains($"#{yopiq.Id}", await Detail(eski));
        await Oqi<SmenaTafsilotDto>(await Tuzat(bosh, yopiq.Id, a1, 107m));

        // Rad etilganlar hech narsani o'zgartirmagan: 100 -> 105 (ikkinchi smena yopilishi), keyin 105 -> 107.
        Assert.Equal(107m, (await Aparatlar(op))[0].TotalLitr);
    }

    [Fact]
    public async Task KeyingiOchiqSmena_NarxSegmentiBoshlanishiYangilanadi_KattaKorsatkichRadEtiladi()
    {
        var (_, op) = await Yarat("ali");
        var admin = await Admin();
        var aparatlar = await Aparatlar(op);
        var birinchi = await YopilganSmena(op, aparatlar);                                       // 1-aparat 100.00 da yopilgan
        var yoqilgi = (await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!.Single(y => y.Nomi == "AI-92");
        await Och(op);
        // Keyingi (ochiq) smenada narx o'zgardi: 1-aparatning birinchi segmenti 100 dan 130 gacha (2-aparat 0 da - segment yo'q).
        await Oqi<YoqilgiTuriDto>(await admin.PutAsJsonAsync($"/yoqilgilar/{yoqilgi.Id}", new YoqilgiTahrirlashDto("AI-92", 13_000, yoqilgi.Rang,
            [new AparatKorsatkichDto(aparatlar[0].Id, 130m), new AparatKorsatkichDto(aparatlar[1].Id, 0m)]), Json));
        var oldin = (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Korsatkichlar.Single();
        Assert.Equal((100m, 130m, 30m), (oldin.Boshi, oldin.Oxiri, oldin.Litr));

        // 130 dan katta qiymat rad etiladi (keyingi segment manfiy bo'lib qolardi); hech narsa o'zgarmaydi.
        await Kut(HttpStatusCode.BadRequest, await Tuzat(admin, birinchi.Id, aparatlar[0].Id, 130.01m));
        Assert.Equal(100m, (await Aparatlar(op))[0].TotalLitr);

        await Oqi<SmenaTafsilotDto>(await Tuzat(admin, birinchi.Id, aparatlar[0].Id, 105m));
        var keyin = (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Korsatkichlar.Single();
        Assert.Equal((105m, 130m, 25m, 305_000L), (keyin.Boshi, keyin.Oxiri, keyin.Litr, keyin.Summa));       // 25 L x 12 200
    }
}
