using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>
/// Ochiq smenada narx o'zgarishi ko'rsatkichini tuzatish (docs 8.14): PUT /smenalar/{id}/narx-korsatkichi. Aparatning oxirgi narx o'zgarishi
/// segmenti tuzatiladi; qiymat segment boshiga teng bo'lsa qator o'chadi. Pult, bak va hisob harakatlariga tegilmaydi.
/// </summary>
public sealed class NarxKorsatkichiTuzatishApiTests : ApiBaza
{
    private static Task<HttpResponseMessage> Tuzat(HttpClient m, int smenaId, int aparatId, decimal qiymat, string sabab = "Pult noto'g'ri yozilgan") =>
        m.PutAsJsonAsync($"/smenalar/{smenaId}/narx-korsatkichi", new KorsatkichTuzatishDto(aparatId, qiymat, sabab), Json);

    private static async Task<YoqilgiTuriDto> Ai92(HttpClient m) => (await m.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!.Single(y => y.Nomi == "AI-92");

    private static Task<HttpResponseMessage> NarxniOzgartir(HttpClient admin, YoqilgiTuriDto y, long narx, params AparatKorsatkichDto[] korsatkichlar) =>
        admin.PutAsJsonAsync($"/yoqilgilar/{y.Id}", new YoqilgiTahrirlashDto(y.Nomi, narx, y.Rang, korsatkichlar), Json);

    private static (int Aparat, decimal Boshi, decimal Oxiri, long Narx, decimal Litr, long Summa)[] Qatorlar(SmenaTafsilotDto t) =>
        t.Korsatkichlar.Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx, k.Litr, k.Summa)).ToArray();

    private static async Task<SmenaTafsilotDto> Joriy(HttpClient m) => await Oqi<SmenaTafsilotDto>(await m.GetAsync("/smenalar/joriy"));

    private static async Task<AuditYozuviDto[]> Tuzatishlar(HttpClient admin) =>
        (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=tuzatish", Json))!;

    private static async Task<int> HarakatSoni(HttpClient admin, int operatorId) =>
        (await Oqi<OperatorHisobDto>(await admin.GetAsync($"/operatorlar/{operatorId}/hisob"))).Harakatlar.Count();

    private sealed record Tayyor(HttpClient Op, HttpClient Bosh, HttpClient Admin, FoydalanuvchiDto Ali, SmenaDto Smena, AparatDto[] Aparatlar);

    /// <summary>
    /// Ali ochiq smena ochgan, AI-92 narxi 12 200 dan 13 000 ga o'zgargan: 1-aparat 100 gacha, 2-aparat 50 gacha eski narxda qayd etilgan
    /// (3-5-aparatlar boshqa yoqilg'i - ularda narx o'zgarishi segmenti yo'q).
    /// </summary>
    private async Task<Tayyor> NarxOzgargan()
    {
        var (ali, op) = await Yarat("ali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var admin = await Admin();
        var aparatlar = await Aparatlar(admin);
        var smena = await Och(op);
        await Oqi<YoqilgiTuriDto>(await NarxniOzgartir(admin, await Ai92(admin), 13_000,
            new AparatKorsatkichDto(aparatlar[0].Id, 100m), new AparatKorsatkichDto(aparatlar[1].Id, 50m)));
        return new Tayyor(op, bosh, admin, ali, smena, aparatlar);
    }

    [Fact]
    public async Task Tuzatish_QatorYangilanadi_PultBakVaHarakatTegilmaydi_AuditYoziladi_YopishdaToGriHisob()
    {
        var t = await NarxOzgargan();
        var (a1, a2) = (t.Aparatlar[0], t.Aparatlar[1]);
        var harakatlar = await HarakatSoni(t.Admin, t.Ali.Id);

        // 1-aparat: narx o'zgarguncha 100 emas, 60 L sotilgan. Qator yangilanadi, narx (12 200) eskicha qoladi.
        var javob = await Oqi<SmenaTafsilotDto>(await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 60m));
        Assert.Equal(t.Smena.Id, javob.Smena.Id);
        Assert.Null(javob.Smena.Tugadi);
        Assert.Equal([(1, 0m, 60m, 12_200L, 60m, 732_000L), (2, 0m, 50m, 12_200L, 50m, 610_000L)], Qatorlar(javob));
        Assert.Equal(Qatorlar(javob), Qatorlar(await Joriy(t.Op)));                                      // bazaga yozilgan

        // Pult va bak o'zgarmaydi, kamomat/ortiqcha harakati yozilmaydi.
        var keyin = await Aparatlar(t.Admin);
        Assert.Equal(t.Aparatlar.Select(a => (a.TotalLitr, a.BakQoldiq)), keyin.Select(a => (a.TotalLitr, a.BakQoldiq)));
        Assert.Equal(harakatlar, await HarakatSoni(t.Admin, t.Ali.Id));

        Assert.Contains(await Tuzatishlar(t.Admin), a => a.Amal == "Ko'rsatkich tuzatildi" && a.Kim == "boshliq" && a.Tur == "tuzatish"
            && a.Tafsilot == $"Smena #{t.Smena.Id} · 1-aparat narx o'zgarishidagi ko'rsatkich 100.00 dan 60.00 ga · sabab: Pult noto'g'ri yozilgan");

        // Yana tuzatish mumkin: 60 -> 70 (qiymat oshishi ham mumkin).
        var ikkinchi = await Oqi<SmenaTafsilotDto>(await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 70m, "  Qayta tekshirildi  "));
        Assert.Equal((70m, 70m, 854_000L), (ikkinchi.Korsatkichlar[0].Oxiri, ikkinchi.Korsatkichlar[0].Litr, ikkinchi.Korsatkichlar[0].Summa));
        Assert.Contains(await Tuzatishlar(t.Admin), a => a.Tafsilot == $"Smena #{t.Smena.Id} · 1-aparat narx o'zgarishidagi ko'rsatkich 60.00 dan 70.00 ga · sabab: Qayta tekshirildi");

        // Yopish: oxirgi segmentlar yangi narxda (13 000), "Oldingi" - tuzatilgan 70 va 50. 1-aparat 250 (180 L), 2-aparat 80 (30 L).
        var yopildi = await Oqi<SmenaDto>(await Yop(t.Op, t.Smena.Id, Oxirgi(t.Aparatlar, 250m, 80m)));
        Assert.Equal((70 * 12_200L + 50 * 12_200L + 180 * 13_000L + 30 * 13_000L, 330m), (yopildi.Savdo, yopildi.JamiLitr));
        var tafsilot = await Oqi<SmenaTafsilotDto>(await t.Admin.GetAsync($"/smenalar/{t.Smena.Id}"));
        Assert.Equal([(1, 0m, 70m, 12_200L, true), (1, 70m, 250m, 13_000L, false), (2, 0m, 50m, 12_200L, true), (2, 50m, 80m, 13_000L, false)],
            tafsilot.Korsatkichlar.Take(4).Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx, k.NarxOzgarishida)));
        var yopiq = await Aparatlar(t.Admin);
        Assert.Equal([(250m, -250m), (80m, -80m)], yopiq.Take(2).Select(a => (a.TotalLitr, a.BakQoldiq)));
    }

    [Fact]
    public async Task QiymatSegmentBoshigaTeng_QatorOchiriladi_YopishdaButunLitrYangiNarxda()
    {
        var t = await NarxOzgargan();
        var a2 = t.Aparatlar[1];

        // 2-aparat: narx o'zgarguncha hech narsa sotilmagan (pult 0 ligicha): 0 litrli qator saqlanmaydi, o'chadi.
        var javob = await Oqi<SmenaTafsilotDto>(await Tuzat(t.Bosh, t.Smena.Id, a2.Id, 0m));
        Assert.Equal([(1, 0m, 100m, 12_200L, 100m, 1_220_000L)], Qatorlar(javob));
        Assert.Equal(Qatorlar(javob), Qatorlar(await Joriy(t.Op)));
        Assert.Contains(await Tuzatishlar(t.Admin), a => a.Amal == "Ko'rsatkich tuzatildi"
            && a.Tafsilot == $"Smena #{t.Smena.Id} · 2-aparat narx o'zgarishidagi ko'rsatkich 50.00 dan 0.00 ga · sabab: Pult noto'g'ri yozilgan");

        // Yopish: 2-aparatning hammasi (80 L) yangi narxda; 1-aparatning segmenti tegilmagan.
        var yopildi = await Oqi<SmenaDto>(await Yop(t.Op, t.Smena.Id, Oxirgi(t.Aparatlar, 250m, 80m)));
        Assert.Equal((1_220_000L + 150 * 13_000L + 80 * 13_000L, 330m), (yopildi.Savdo, yopildi.JamiLitr));
        var tafsilot = await Oqi<SmenaTafsilotDto>(await t.Admin.GetAsync($"/smenalar/{t.Smena.Id}"));
        Assert.Equal([(1, 0m, 100m, 12_200L, true), (1, 100m, 250m, 13_000L, false), (2, 0m, 80m, 13_000L, false)],
            tafsilot.Korsatkichlar.Take(3).Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx, k.NarxOzgarishida)));
        Assert.Equal([(250m, -250m), (80m, -80m)], (await Aparatlar(t.Admin)).Take(2).Select(a => (a.TotalLitr, a.BakQoldiq)));
    }

    [Fact]
    public async Task IkkiMartaNarxOzgargan_FaqatOxirgiSegmentTuzatiladi()
    {
        var t = await NarxOzgargan();
        var (a1, a2) = (t.Aparatlar[0], t.Aparatlar[1]);
        // Ikkinchi o'zgarish: 1-aparat 150 gacha 13 000 da (xato: aslida 120), 2-aparat o'zgarmagan (segment yozilmaydi).
        await Oqi<YoqilgiTuriDto>(await NarxniOzgartir(t.Admin, await Ai92(t.Admin), 14_000, new AparatKorsatkichDto(a1.Id, 150m), new AparatKorsatkichDto(a2.Id, 50m)));
        Assert.Equal([(1, 0m, 100m, 12_200L), (1, 100m, 150m, 13_000L), (2, 0m, 50m, 12_200L)],
            (await Joriy(t.Op)).Korsatkichlar.Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx)));

        // Oxirgi segmentning boshidan (100) kichik qiymat rad etiladi: oldingi segmentga tegib bo'lmaydi.
        await Kut(HttpStatusCode.BadRequest, await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 99.99m));

        await Oqi<SmenaTafsilotDto>(await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 120m));
        Assert.Equal([(1, 0m, 100m, 12_200L), (1, 100m, 120m, 13_000L), (2, 0m, 50m, 12_200L)],
            (await Joriy(t.Op)).Korsatkichlar.Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx)));

        // Oxirgi segment o'chadi (100 ga teng) - 1-aparatda birinchi segment qoladi, keyingi yopish 100 dan boshlanadi.
        await Oqi<SmenaTafsilotDto>(await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 100m));
        Assert.Equal([(1, 0m, 100m, 12_200L), (2, 0m, 50m, 12_200L)], (await Joriy(t.Op)).Korsatkichlar.Select(k => (k.AparatRaqam, k.Boshi, k.Oxiri, k.Narx)));
        var yopildi = await Oqi<SmenaDto>(await Yop(t.Op, t.Smena.Id, Oxirgi(t.Aparatlar, 130m, 60m)));
        Assert.Equal(100 * 12_200L + 30 * 14_000L + 50 * 12_200L + 10 * 14_000L, yopildi.Savdo);
    }

    [Fact]
    public async Task Rad_Holatlari_400_404_409_HechNarsaOzgarmaydi()
    {
        var t = await NarxOzgargan();
        var (a1, a3) = (t.Aparatlar[0], t.Aparatlar[2]);                 // 3-aparat boshqa yoqilg'i: narx o'zgarishi segmenti yo'q
        var oldin = Qatorlar(await Joriy(t.Op));
        var auditOldin = (await Tuzatishlar(t.Admin)).Length;

        await Kut(HttpStatusCode.BadRequest, await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 60m, "  "));          // sabab majburiy
        await Kut(HttpStatusCode.BadRequest, await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 60m, ""));
        await Kut(HttpStatusCode.BadRequest, await t.Bosh.PutAsJsonAsync($"/smenalar/{t.Smena.Id}/narx-korsatkichi", new { aparatId = a1.Id, qiymat = 60m }, Json));   // sabab umuman yo'q
        var kichik = await Tuzat(t.Bosh, t.Smena.Id, a1.Id, -1m);                                           // segment boshidan kichik
        await Kut(HttpStatusCode.BadRequest, kichik);
        Assert.Equal("1-aparat: yangi pult ko'rsatkichi oldingi ko'rsatkich (0.00) dan kichik bo'lishi mumkin emas.", await Detail(kichik));
        var teng = await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 100m);                                            // joriy ko'rsatkichga teng
        await Kut(HttpStatusCode.BadRequest, teng);
        Assert.Equal("Yangi ko'rsatkich joriy ko'rsatkichdan farq qilishi kerak.", await Detail(teng));
        await Kut(HttpStatusCode.BadRequest, await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 100.004m));             // yaxlitlanganda joriy ko'rsatkichga teng
        await Kut(HttpStatusCode.NotFound, await Tuzat(t.Bosh, t.Smena.Id, 9999, 60m));                     // aparat yo'q
        await Kut(HttpStatusCode.NotFound, await Tuzat(t.Bosh, 9999, a1.Id, 60m));                          // smena yo'q
        var yoq = await Tuzat(t.Bosh, t.Smena.Id, a3.Id, 5m);                                               // aparatda narx o'zgarishi segmenti yo'q
        await Kut(HttpStatusCode.Conflict, yoq);
        Assert.Equal("Bu aparatda ochiq smenada narx o'zgarishi ko'rsatkichi yo'q.", await Detail(yoq));

        Assert.Equal(oldin, Qatorlar(await Joriy(t.Op)));                                                    // hech narsa o'zgarmadi
        Assert.Equal(auditOldin, (await Tuzatishlar(t.Admin)).Length);                                      // audit yozilmadi
    }

    [Fact]
    public async Task Ruxsat_403_Token401_YopilganSmena409()
    {
        var t = await NarxOzgargan();
        var a1 = t.Aparatlar[0];

        await Kut(HttpStatusCode.Forbidden, await Tuzat(t.Op, t.Smena.Id, a1.Id, 60m));                         // operatorda KorsatkichTuzatish yo'q
        await Kut(HttpStatusCode.Unauthorized, await Tuzat(Ilova.CreateClient(), t.Smena.Id, a1.Id, 60m));
        var ruxsatsiz = Enum.GetValues<Ruxsat>().Where(r => r != Ruxsat.KorsatkichTuzatish).ToArray();
        var (_, boshRuxsatsiz) = await Yarat("boshliq2", Rol.Boshliq, ruxsatsiz);                                // boshqa hamma ruxsat bor, bu yo'q
        await Kut(HttpStatusCode.Forbidden, await Tuzat(boshRuxsatsiz, t.Smena.Id, a1.Id, 60m));
        Assert.Equal(100m, (await Joriy(t.Op)).Korsatkichlar[0].Oxiri);                                          // rad etilganlar tegmagan

        // Ruxsat berilgan operator tuzata oladi.
        var (_, tuzatuvchi) = await Yarat("tuzatuvchi", Rol.Operator, [Ruxsat.KorsatkichTuzatish]);
        await Oqi<SmenaTafsilotDto>(await Tuzat(tuzatuvchi, t.Smena.Id, a1.Id, 60m));
        Assert.Equal(60m, (await Joriy(t.Op)).Korsatkichlar[0].Oxiri);

        // Smena yopilgach bu endpoint 409 (yopilgan smena uchun - PUT /smenalar/{id}/korsatkich); aparat yo'qligidan oldin smena tekshiriladi.
        await Oqi<SmenaDto>(await Yop(t.Op, t.Smena.Id, Oxirgi(t.Aparatlar, 250m, 80m)));
        var yopiq = await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 70m);
        await Kut(HttpStatusCode.Conflict, yopiq);
        Assert.Contains("faqat ochiq smenada", await Detail(yopiq));
        await Kut(HttpStatusCode.Conflict, await Tuzat(t.Bosh, t.Smena.Id, 9999, 70m));
        Assert.Equal(60m, (await Oqi<SmenaTafsilotDto>(await t.Admin.GetAsync($"/smenalar/{t.Smena.Id}"))).Korsatkichlar[0].Oxiri);
        Assert.Equal(250m, (await Aparatlar(t.Admin))[0].TotalLitr);

        // Ochiq smena umuman yo'q: eski smenaning id si bilan ham 409.
        await Kut(HttpStatusCode.Conflict, await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 70m));
    }

    [Fact]
    public async Task SmenaOzgardi_FaqatMuvaffaqiyatliTuzatishdan_KuzatuvchilarGa()
    {
        var t = await NarxOzgargan();
        var a1 = t.Aparatlar[0];
        var kelgan = new List<SmenaDto>();
        var hub = await HubUlan(t.Admin, h => h.On<SmenaDto>("SmenaOzgardi", d => { lock (kelgan) kelgan.Add(d); }));
        try
        {
            await Kut(HttpStatusCode.BadRequest, await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 100m));                // rad etilgan - xabar yo'q
            await Oqi<SmenaTafsilotDto>(await Tuzat(t.Bosh, t.Smena.Id, a1.Id, 60m));
            for (var i = 0; i < 80 && Soni(kelgan) == 0; i++) await Task.Delay(100);
            await Task.Delay(300);                                                                              // ortiqcha xabar kelmasligini tekshirish uchun

            Assert.Equal(1, Soni(kelgan));
            lock (kelgan) Assert.Equal((t.Smena.Id, true), (kelgan[0].Id, kelgan[0].Tugadi is null));
        }
        finally { await hub.DisposeAsync(); }
    }

    private static int Soni(List<SmenaDto> royxat) { lock (royxat) return royxat.Count; }

    /// <summary>
    /// 2026-10-17: DIZEL ECO (15 500) aparati (6-aparat: pult 109 797.58, bak 5 017.83) mijozdagi kabi; narx 16 000 ga o'zgarganda
    /// 109 797.58 o'rniga 1 099 797.58 yozilgan (990 000 L x 15 500). Aparatlar - yangi aparat bilan (6-aparat oxirgisi).
    /// </summary>
    private async Task<(HttpClient Op, HttpClient Bosh, HttpClient Admin, SmenaDto Smena, AparatDto[] Aparatlar, AparatDto Eco)> XatoYozilgan()
    {
        var (_, op) = await Yarat("ali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var admin = await Admin();
        var yoqilgi = await Oqi<YoqilgiTuriDto>(await admin.PostAsJsonAsync("/yoqilgilar", new YoqilgiYaratishDto("DIZEL ECO", 15_500, "#228B22"), Json));
        var eco = await Oqi<AparatDto>(await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(6, yoqilgi.Id, 109_797.58m, 5_017.83m), Json));
        var aparatlar = await Aparatlar(admin);
        var smena = await Och(op);
        await Oqi<YoqilgiTuriDto>(await NarxniOzgartir(admin, yoqilgi, 16_000, new AparatKorsatkichDto(eco.Id, 1_099_797.58m)));
        return (op, bosh, admin, smena, aparatlar, eco);
    }

    [Fact]
    public async Task Ssenariy_2026_10_17_XatoKorsatkichTuzatiladi_QatorOchadi_SmenaYopiladi_ToliqYangiNarxda()
    {
        var e = await XatoYozilgan();
        var xato = Assert.Single((await Joriy(e.Op)).Korsatkichlar);
        Assert.Equal((6, 109_797.58m, 1_099_797.58m, 15_500L, 990_000.00m, 15_345_000_000L), (xato.AparatRaqam, xato.Boshi, xato.Oxiri, xato.Narx, xato.Litr, xato.Summa));
        var yopish = Oxirgi(e.Aparatlar, 0, 0, 0, 0, 0, 100m);                                                 // 6-aparat 109 897.58 (100 L)

        // Tuzatishsiz yopib bo'lmaydi: yopish ko'rsatkichi 1 099 797.58 dan kichik bo'lolmaydi.
        await Kut(HttpStatusCode.BadRequest, await Yop(e.Op, e.Smena.Id, yopish, naqd: 1_700_000));

        // Narx o'zgarguncha savdo bo'lmagan: pult 109 797.58 ligicha - qator o'chadi.
        var javob = await Oqi<SmenaTafsilotDto>(await Tuzat(e.Bosh, e.Smena.Id, e.Eco.Id, 109_797.58m, "Pult 10 marta ko'p yozilgan"));
        Assert.Empty(javob.Korsatkichlar);
        Assert.Empty((await Joriy(e.Op)).Korsatkichlar);
        Assert.Contains(await Tuzatishlar(e.Admin), a => a.Amal == "Ko'rsatkich tuzatildi"
            && a.Tafsilot == $"Smena #{e.Smena.Id} · 6-aparat narx o'zgarishidagi ko'rsatkich 1 099 797.58 dan 109 797.58 ga · sabab: Pult 10 marta ko'p yozilgan");
        var eco = (await Aparatlar(e.Admin)).Single(a => a.Raqam == 6);
        Assert.Equal((109_797.58m, 5_017.83m), (eco.TotalLitr, eco.BakQoldiq));                              // pult va bak tegilmagan

        // Smena yopiladi: 6-aparat to'liq yangi narxda (16 000), bakdan 100 L ayriladi. Kutilgan 100 000 + 1 600 000, farq 0.
        var yopildi = await Oqi<SmenaDto>(await Yop(e.Op, e.Smena.Id, yopish, naqd: 1_700_000));
        Assert.Equal((1_600_000L, 100m, 0L), (yopildi.Savdo, yopildi.JamiLitr, yopildi.Farq));
        eco = (await Aparatlar(e.Admin)).Single(a => a.Raqam == 6);
        Assert.Equal((109_897.58m, 4_917.83m), (eco.TotalLitr, eco.BakQoldiq));
        var tafsilot = await Oqi<SmenaTafsilotDto>(await e.Admin.GetAsync($"/smenalar/{e.Smena.Id}"));
        var s = Assert.Single(tafsilot.Korsatkichlar, k => k.AparatRaqam == 6);
        Assert.Equal((109_797.58m, 109_897.58m, 16_000L, 100m, 1_600_000L, false), (s.Boshi, s.Oxiri, s.Narx, s.Litr, s.Summa, s.NarxOzgarishida));
    }

    [Fact]
    public async Task Ssenariy_2026_10_17_NarxOzgarguncha100LSotilgan_QatorQisqaradi_IkkiNarxdaHisoblanadi()
    {
        var e = await XatoYozilgan();

        // Narx o'zgarguncha 100 L sotilgan edi (pult 109 897.58): qator o'chmaydi, 100 x 15 500 ga qisqaradi.
        var javob = await Oqi<SmenaTafsilotDto>(await Tuzat(e.Bosh, e.Smena.Id, e.Eco.Id, 109_897.58m));
        Assert.Equal([(6, 109_797.58m, 109_897.58m, 15_500L, 100.00m, 1_550_000L)], Qatorlar(javob));

        // Yopish: yana 100 L (109 997.58) yangi narxda: 1 550 000 + 1 600 000. Bakdan jami 200 L.
        var yopildi = await Oqi<SmenaDto>(await Yop(e.Op, e.Smena.Id, Oxirgi(e.Aparatlar, 0, 0, 0, 0, 0, 200m), naqd: 3_250_000));
        Assert.Equal((3_150_000L, 200m, 0L), (yopildi.Savdo, yopildi.JamiLitr, yopildi.Farq));
        var eco = (await Aparatlar(e.Admin)).Single(a => a.Raqam == 6);
        Assert.Equal((109_997.58m, 4_817.83m), (eco.TotalLitr, eco.BakQoldiq));
        var tafsilot = await Oqi<SmenaTafsilotDto>(await e.Admin.GetAsync($"/smenalar/{e.Smena.Id}"));
        Assert.Equal([(109_797.58m, 109_897.58m, 15_500L, true), (109_897.58m, 109_997.58m, 16_000L, false)],
            tafsilot.Korsatkichlar.Where(k => k.AparatRaqam == 6).Select(k => (k.Boshi, k.Oxiri, k.Narx, k.NarxOzgarishida)));
    }
}
