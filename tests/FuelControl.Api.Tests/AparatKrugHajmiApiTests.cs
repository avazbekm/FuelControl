using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>
/// Pult krugi (docs 8.16): Aparat.KrugHajmi - pult 0 ga qaytadigan hajm (null - krug yo'q). Yaratishda null/0 - yo'q; tahrirlashda null -
/// o'zgarmaydi (eski klient maydonni yubormaydi), 0 - o'chadi, &gt; 0 - o'rnatiladi; manfiy - 400. Hisob formulalari umumiy litrda qoladi.
/// </summary>
public sealed class AparatKrugHajmiApiTests : ApiBaza
{
    private static Task<HttpResponseMessage> Tahrir(HttpClient m, AparatDto a, decimal? krug, int? raqam = null, int? yoqilgiId = null, decimal? total = null, string? sabab = null) =>
        m.PutAsJsonAsync($"/aparatlar/{a.Id}", new AparatTahrirlashDto(raqam ?? a.Raqam, yoqilgiId ?? a.YoqilgiTuriId, total, null, sabab, krug), Json);

    private static async Task<AparatDto> Bitta(HttpClient m, int id) => (await Aparatlar(m)).Single(a => a.Id == id);

    private static async Task<AuditYozuviDto[]> Audit(HttpClient admin, string q) =>
        (await admin.GetFromJsonAsync<AuditYozuviDto[]>($"/audit?q={Uri.EscapeDataString(q)}", Json))!;

    [Fact]
    public async Task Yaratish_NullVa0KrugYoq_MusbatSaqlanadi_Yaxlitlanadi_ManfiyRadEtiladi_EskiKlient()
    {
        var admin = await Admin();
        var yoqilgi = (await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))![0];
        Task<HttpResponseMessage> Yarat(int raqam, decimal? krug) =>
            admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(raqam, yoqilgi.Id, 100m, 50m, krug), Json);

        Assert.Null((await Oqi<AparatDto>(await Yarat(6, null))).KrugHajmi);
        Assert.Null((await Oqi<AparatDto>(await Yarat(7, 0m))).KrugHajmi);                                   // 0 - krug yo'q (null bo'lib saqlanadi)
        var krugli = await Oqi<AparatDto>(await Yarat(8, 10_000m));
        Assert.Equal((10_000m, 100m, 50m), (krugli.KrugHajmi, krugli.TotalLitr, krugli.BakQoldiq));            // pult/bak umumiy litrda, krug ularga tegmaydi
        Assert.Null((await Oqi<AparatDto>(await Yarat(9, 0.004m))).KrugHajmi);                                // 2 xonaga yaxlitlanganda 0
        Assert.Equal(10_000.01m, (await Oqi<AparatDto>(await Yarat(10, 10_000.005m))).KrugHajmi);

        var manfiy = await Yarat(11, -1m);
        await Kut(HttpStatusCode.BadRequest, manfiy);
        Assert.Equal("Krug hajmi manfiy bo'lmasligi kerak.", await Detail(manfiy));
        Assert.DoesNotContain(await Aparatlar(admin), a => a.Raqam == 11);                                    // yaratilmadi

        // Eski klient: maydonsiz so'rov - krug yo'q.
        var eski = await admin.PostAsJsonAsync("/aparatlar", new { raqam = 12, yoqilgiTuriId = yoqilgi.Id, boshlangichTotalLitr = 1m, boshlangichBakQoldiq = 2m }, Json);
        Assert.Null((await Oqi<AparatDto>(eski)).KrugHajmi);

        // GET /aparatlar ham krugHajmi'ni qaytaradi; audit matnida krug (faqat o'rnatilganda).
        var royxat = await Aparatlar(admin);
        Assert.Equal([null, 10_000m, 10_000.01m], royxat.Where(a => a.Raqam is 7 or 8 or 10).OrderBy(a => a.Raqam).Select(a => a.KrugHajmi));
        var audit = await Audit(admin, "Aparat yaratildi");
        Assert.Contains(audit, a => a.Tafsilot == $"8-aparat, {yoqilgi.Nomi} · pult 100.00 L · bak 50.00 L · krug 10 000 L");
        Assert.Contains(audit, a => a.Tafsilot == $"6-aparat, {yoqilgi.Nomi} · pult 100.00 L · bak 50.00 L");
    }

    [Fact]
    public async Task Tahrirlash_NullOzgarmaydi_0Ochiradi_MusbatOrnatadi_ManfiyRad_AuditFaqatOzgarganda()
    {
        var admin = await Admin();
        var a5 = (await Aparatlar(admin))[4];
        Assert.Null(a5.KrugHajmi);
        var nom = a5.YoqilgiNomi;

        var d1 = await Oqi<AparatDto>(await Tahrir(admin, a5, 10_000m));                                       // > 0 - o'rnatadi
        Assert.Equal((10_000m, a5.TotalLitr, a5.BakQoldiq), (d1.KrugHajmi, d1.TotalLitr, d1.BakQoldiq));
        Assert.Equal(10_000m, (await Oqi<AparatDto>(await Tahrir(admin, a5, null))).KrugHajmi);                // maydon null - o'zgarmaydi
        var eski = await admin.PutAsJsonAsync($"/aparatlar/{a5.Id}", new { raqam = a5.Raqam, yoqilgiTuriId = a5.YoqilgiTuriId }, Json);
        Assert.Equal(10_000m, (await Oqi<AparatDto>(eski)).KrugHajmi);                                         // eski klient maydonni yubormadi - o'chib qolmadi
        Assert.Equal(10_000m, (await Oqi<AparatDto>(await Tahrir(admin, a5, 10_000m))).KrugHajmi);             // o'sha qiymat - o'zgarish yo'q
        Assert.Equal(20_000m, (await Oqi<AparatDto>(await Tahrir(admin, a5, 20_000m))).KrugHajmi);

        var manfiy = await Tahrir(admin, a5, -5m);
        await Kut(HttpStatusCode.BadRequest, manfiy);
        Assert.Equal("Krug hajmi manfiy bo'lmasligi kerak.", await Detail(manfiy));
        Assert.Equal(20_000m, (await Bitta(admin, a5.Id)).KrugHajmi);                                          // rad etilgani o'zgartirmadi

        Assert.Null((await Oqi<AparatDto>(await Tahrir(admin, a5, 0m))).KrugHajmi);                            // 0 - o'chiradi
        Assert.Null((await Bitta(admin, a5.Id)).KrugHajmi);
        Assert.Null((await Oqi<AparatDto>(await Tahrir(admin, a5, 0m))).KrugHajmi);                            // allaqachon yo'q - o'zgarish yo'q

        // Audit faqat haqiqatan o'zgarganda: 10 000, 20 000, yo'q. Raqam ham o'zgarsa - bitta yozuv.
        var krugAudit = (await Audit(admin, "krug")).Where(x => x.Amal == "Aparat o'zgartirildi").OrderBy(x => x.Id).ToArray();
        Assert.Equal([$"5-aparat · yoqilg'i {nom} · krug 10 000 L", $"5-aparat · yoqilg'i {nom} · krug 20 000 L", $"5-aparat · yoqilg'i {nom} · krug yo'q"],
            krugAudit.Select(x => x.Tafsilot));
        Assert.All(krugAudit, x => Assert.Equal("sozlama", x.Tur));
        await Oqi<AparatDto>(await Tahrir(admin, a5, 5_000.5m, raqam: 15));
        Assert.Contains(await Audit(admin, "krug"), x => x.Tafsilot == $"5-aparat raqami 15 ga o'zgardi · yoqilg'i {nom} · krug 5 000.50 L");
        Assert.Equal(5_000.5m, (await Bitta(admin, a5.Id)).KrugHajmi);
    }

    [Fact]
    public async Task OchiqSmena_KrugOzgaradi_TotalLitrVaYoqilgi409Qoladi_HisobUmumiyLitrda()
    {
        var (_, op) = await Yarat("ali");
        var admin = await Admin();
        var yoqilgilar = (await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!;
        var ai92 = yoqilgilar.Single(y => y.Nomi == "AI-92");
        var aparatlar = await Aparatlar(admin);
        var (a1, a2) = (aparatlar[0], aparatlar[1]);                                                          // AI-92 aparatlari
        var smena = await Och(op);
        await Oqi<YoqilgiTuriDto>(await admin.PutAsJsonAsync($"/yoqilgilar/{ai92.Id}",
            new YoqilgiTahrirlashDto(ai92.Nomi, 13_000, ai92.Rang, [new AparatKorsatkichDto(a1.Id, 100m), new AparatKorsatkichDto(a2.Id, 50m)]), Json));

        // Ochiq smenada narx o'zgarishi segmenti bor aparatning krugi o'zgaradi (faqat ko'rinish sozlamasi).
        Assert.Equal(5_000m, (await Oqi<AparatDto>(await Tahrir(admin, a1, 5_000m))).KrugHajmi);
        // 409 qoidasi TotalLitr va yoqilg'iga tegishli: butun so'rov rad etiladi, krug ham o'zgarmaydi.
        await Kut(HttpStatusCode.Conflict, await Tahrir(admin, a1, 7_000m, total: 5m, sabab: "xato"));
        await Kut(HttpStatusCode.Conflict, await Tahrir(admin, a1, 7_000m, yoqilgiId: yoqilgilar.First(y => y.Id != ai92.Id).Id));
        var hozir = await Bitta(admin, a1.Id);
        Assert.Equal((5_000m, a1.TotalLitr, a1.YoqilgiTuriId), (hozir.KrugHajmi, hozir.TotalLitr, hozir.YoqilgiTuriId));

        // Hisob formulalari umumiy litrda: pult 10 000 dan oshgan umumiy qiymat ham oddiy hisoblanadi, krug bunga aralashmaydi.
        var yopildi = await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 12_345.67m, 50m)));
        Assert.Equal(1_220_000L + 159_193_710L + 610_000L, yopildi.Savdo);                                     // 100 x 12 200 + 12 245.67 x 13 000 + 50 x 12 200
        Assert.Equal(12_395.67m, yopildi.JamiLitr);
        var yopiq = await Bitta(admin, a1.Id);
        Assert.Equal((12_345.67m, -12_345.67m, 5_000m), (yopiq.TotalLitr, yopiq.BakQoldiq, yopiq.KrugHajmi));  // krug yopilgandan keyin ham joyida
    }

    [Fact]
    public async Task Ruxsat_SozlamalarKerak_VaAparatOzgardiHodisasiKrugBilan()
    {
        var (_, op) = await Yarat("ali");
        var admin = await Admin();
        var a5 = (await Aparatlar(admin))[4];
        await Kut(HttpStatusCode.Forbidden, await Tahrir(op, a5, 10_000m));                                    // operatorda Sozlamalar ruxsati yo'q
        Assert.Null((await Bitta(admin, a5.Id)).KrugHajmi);

        var kelgan = new List<AparatDto>();
        var hub = await HubUlan(admin, h => h.On<AparatDto>("AparatOzgardi", d => { lock (kelgan) kelgan.Add(d); }));
        try
        {
            await Oqi<AparatDto>(await Tahrir(admin, a5, 10_000m));
            var yangi = await Oqi<AparatDto>(await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(6, a5.YoqilgiTuriId, 0m, 0m, 2_500m), Json));
            for (var i = 0; i < 80 && Soni(kelgan) < 2; i++) await Task.Delay(100);

            Assert.Equal(2, Soni(kelgan));
            lock (kelgan)
            {
                Assert.Equal((a5.Id, 10_000m), (kelgan[0].Id, kelgan[0].KrugHajmi));                          // tahrirlash: yangi qiymat bilan
                Assert.Equal((yangi.Id, 2_500m), (kelgan[1].Id, kelgan[1].KrugHajmi));                       // yaratish: krug bilan
            }
        }
        finally { await hub.DisposeAsync(); }
    }

    private static int Soni(List<AparatDto> royxat) { lock (royxat) return royxat.Count; }
}
