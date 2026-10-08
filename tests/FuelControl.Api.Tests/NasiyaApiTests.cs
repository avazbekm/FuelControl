using System.Net;
using System.Net.Http.Json;
using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuelControl.Api.Tests;

public sealed class NasiyaApiTests : ApiBaza
{
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private static NasiyaYaratishDto Yangi(string ism = "Bobur Aliyev", long summa = 600_000, int kun = 7, string tel = "+998 97 700 80 90", string raqam = "01 H 202 MA") =>
        new(ism, tel, raqam, summa, Bugun.AddDays(kun), "izoh");

    private static Task<HttpResponseMessage> Yoz(HttpClient m, NasiyaYaratishDto d) => m.PostAsJsonAsync("/nasiyalar", d, Json);

    private static Task<HttpResponseMessage> Qaytar(HttpClient m, int id, long summa, TolovTuri usul = TolovTuri.Naqd, bool smenaga = true) =>
        m.PostAsJsonAsync($"/nasiyalar/{id}/qaytish", new NasiyaQaytishiYaratishDto(summa, usul, smenaga, null), Json);

    [Fact]
    public async Task Yozish_OchiqSmenaMajburiy_DtoToldiriladi()
    {
        var (_, op) = await Yarat("ali");
        await Kut(HttpStatusCode.Conflict, await Yoz(op, Yangi()));            // ochiq smena yo'q

        var smena = await Och(op);
        var n = await Oqi<NasiyaDto>(await Yoz(op, Yangi()));

        Assert.Equal((smena.Id, "ali", "Bobur Aliyev", 600_000L, 0L, 600_000L), (n.SmenaId, n.OperatorIsmi, n.MijozIsmi, n.Summa, n.Qaytgan, n.Qoldiq));
        Assert.Equal((NasiyaHolati.Faol, 7, null), (n.Holati, n.MuddatgachaKun, n.Yopildi));
        Assert.Equal("01 H 202 MA", n.MashinaRaqami);
        Assert.Equal(600_000, (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Smena.NasiyaJami);

        var admin = await Admin();
        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=nasiya", Json))!;
        var yozuv = Assert.Single(audit);
        Assert.Equal(("Nasiya yozildi", $"Bobur Aliyev · 01 H 202 MA · 600 000 · {Bugun.AddDays(7):dd.MM} gacha"), (yozuv.Amal, yozuv.Tafsilot));
    }

    [Fact]
    public async Task Yozish_Validatsiya_400()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, Yangi(tel: "", raqam: " ")));              // telefon yoki raqam kerak
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, Yangi(kun: -1)));                           // muddat o'tgan sana
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, Yangi(summa: 0)));
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, Yangi(ism: " ")));
        await Kut(HttpStatusCode.Created, await Yoz(op, Yangi(kun: 0, tel: "", raqam: "01 A 000 AA")));  // muddat bugun - mumkin; faqat raqam ham yetadi
        var (_, ruxsatsiz) = await Yarat("sardor", ruxsatlar: [Ruxsat.Savdo, Ruxsat.Nasiyalar]);        // NasiyaYozish yo'q
        await Kut(HttpStatusCode.Forbidden, await Yoz(ruxsatsiz, Yangi()));
    }

    /// <summary>API o'tgan muddatli nasiya yozishga yo'l qo'ymaydi - muddati o'tgan va yopilgan nasiyalarni bazaga to'g'ridan-to'g'ri qo'shamiz.</summary>
    private async Task<(int Otgan, int Yopilgan)> EskiNasiyalar(int smenaId, int operatorId)
    {
        using var scope = Ilova.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FuelControlDbContext>();
        var otgan = new Nasiya { SmenaId = smenaId, OperatorId = operatorId, KimYozdi = "ali", MijozIsmi = "Sherzod Qodirov", Telefon = "+998 91 888 77 66", MashinaRaqami = "40 C 919 DA",
            Summa = 180_000, Muddat = Bugun.AddDays(-6), Yozildi = DateTime.UtcNow.AddDays(-14) };
        var yopilgan = new Nasiya { SmenaId = smenaId, OperatorId = operatorId, KimYozdi = "ali", MijozIsmi = "Davron Rahimov", Telefon = "+998 90 000 11 22", MashinaRaqami = "10 B 100 BB",
            Summa = 90_000, Qaytgan = 90_000, Muddat = Bugun.AddDays(-20), Yozildi = DateTime.UtcNow.AddDays(-30), Yopildi = DateTime.UtcNow.AddDays(-25) };
        db.Nasiyalar.AddRange(otgan, yopilgan);
        await db.SaveChangesAsync();
        return (otgan.Id, yopilgan.Id);
    }

    [Fact]
    public async Task Royxat_HolatFiltri_Qidiruv_Xulosa_MuddatiOtgan()
    {
        var (ali, op) = await Yarat("ali");
        var smena = await Och(op);
        var (otganId, yopilganId) = await EskiNasiyalar(smena.Id, ali.Id);
        var faol = await Oqi<NasiyaDto>(await Yoz(op, Yangi()));

        var hammasi = await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar"));
        Assert.Equal(3, hammasi.Royxat.Length);
        // Tartib: qarzi borlar muddat bo'yicha (muddati o'tgani birinchi), keyin yopilganlar.
        Assert.Equal([otganId, faol.Id, yopilganId], hammasi.Royxat.Select(x => x.Id));
        var o = hammasi.Royxat[0];
        Assert.Equal((NasiyaHolati.MuddatiOtgan, -6, 180_000L), (o.Holati, o.MuddatgachaKun, o.Qoldiq));
        Assert.Equal(NasiyaHolati.Yopilgan, hammasi.Royxat[2].Holati);

        Assert.Equal(2, (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?holat=faol"))).Royxat.Length);        // qarzi bor hammasi (muddati o'tgani ham)
        Assert.Equal([otganId], (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?holat=otgan"))).Royxat.Select(x => x.Id));
        Assert.Equal([yopilganId], (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?holat=yopilgan"))).Royxat.Select(x => x.Id));
        await Kut(HttpStatusCode.BadRequest, await op.GetAsync("/nasiyalar?holat=xato"));

        // Qidiruv: ism, telefon (probel/+/tire e'tiborsiz), mashina raqami (probelsiz, kichik harf).
        Assert.Equal([faol.Id], (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?q=bobur"))).Royxat.Select(x => x.Id));
        Assert.Equal([faol.Id], (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?q=97%20700%2080"))).Royxat.Select(x => x.Id));
        Assert.Equal([otganId], (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?q=40c919"))).Royxat.Select(x => x.Id));
        Assert.Empty((await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?q=topilmaydi"))).Royxat);

        // Xulosa filtrga bog'liq emas: faol qarz 780 000 (2 ta), shundan muddati o'tgan 180 000 (1 ta).
        var x = (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar?holat=yopilgan&q=davron"))).Xulosa;
        Assert.Equal((780_000L, 2, 180_000L, 1), (x.FaolQarz, x.FaolSoni, x.MuddatiOtgan, x.MuddatiOtganSoni));
        Assert.Equal(600_000, x.OyBerilgan);                     // eski nasiyalar o'tgan oy(lar)da yozilgan; bu oy faqat bittasi
        Assert.Equal(1, x.OyBerilganSoni);

        var tafsilot = await Oqi<NasiyaTafsilotDto>(await op.GetAsync($"/nasiyalar/{faol.Id}"));
        Assert.Equal((faol.Id, 0), (tafsilot.Nasiya.Id, tafsilot.Qaytishlar.Length));
        await Kut(HttpStatusCode.NotFound, await op.GetAsync("/nasiyalar/9999"));
        var (_, korolmaydi) = await Yarat("sardor", ruxsatlar: [Ruxsat.Savdo]);                          // Nasiyalar ruxsati yo'q
        await Kut(HttpStatusCode.Forbidden, await korolmaydi.GetAsync("/nasiyalar"));
    }

    [Fact]
    public async Task Qaytish_UsullarOrtiqchaVaSmenaHisobigaQoidalari()
    {
        var (_, op) = await Yarat("ali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var smena = await Och(op);
        var n = await Oqi<NasiyaDto>(await Yoz(op, Yangi()));
        var n2 = await Oqi<NasiyaDto>(await Yoz(op, Yangi("Sherzod", 200_000)));

        var q1 = await Oqi<NasiyaDto>(await Qaytar(op, n.Id, 100_000));                                  // naqd, smena hisobiga
        Assert.Equal((100_000L, 500_000L, NasiyaHolati.Faol), (q1.Qaytgan, q1.Qoldiq, q1.Holati));
        await Oqi<NasiyaDto>(await Qaytar(op, n.Id, 50_000, TolovTuri.Plastik));
        var q3 = await Oqi<NasiyaDto>(await Qaytar(op, n.Id, 50_000, TolovTuri.Depozit));
        Assert.Equal(200_000L, q3.Qaytgan);
        Assert.Equal(200_000, (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Smena.QaytganNasiya);   // 100 000 + 50 000 + 50 000

        await Kut(HttpStatusCode.BadRequest, await Qaytar(op, n.Id, 400_001));                            // qoldiq 400 000 dan ortiq
        await Kut(HttpStatusCode.BadRequest, await Qaytar(op, n.Id, 0));
        await Kut(HttpStatusCode.NotFound, await Qaytar(op, 9999, 1));

        // Smena hisobiga yozilmaydigan qaytishni faqat boshliq ("Smenalar") yoza oladi; smena yig'indisiga ta'sir qilmaydi.
        var rad = await Qaytar(op, n.Id, 100_000, smenaga: false);
        await Kut(HttpStatusCode.Forbidden, rad);
        var boshliqniki = await Oqi<NasiyaDto>(await Qaytar(bosh, n.Id, 100_000, smenaga: false));
        Assert.Equal(300_000L, boshliqniki.Qaytgan);
        Assert.Equal(200_000, (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Smena.QaytganNasiya);

        // To'liq qaytarilsa - yopiladi.
        var yopilgan = await Oqi<NasiyaDto>(await Qaytar(op, n.Id, 300_000));
        Assert.Equal((0L, NasiyaHolati.Yopilgan), (yopilgan.Qoldiq, yopilgan.Holati));
        Assert.NotNull(yopilgan.Yopildi);
        var tafsilot = await Oqi<NasiyaTafsilotDto>(await op.GetAsync($"/nasiyalar/{n.Id}"));
        Assert.Equal(5, tafsilot.Qaytishlar.Length);
        Assert.Contains(tafsilot.Qaytishlar, q => q.SmenaId is null && q.Usul == TolovTuri.Naqd);
        Assert.Equal(4, tafsilot.Qaytishlar.Count(q => q.SmenaId == smena.Id));

        var admin = await Admin();
        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?q=Qarz%20qaytdi&limit=50", Json))!;
        Assert.Contains(audit, a => a.Tafsilot == "Bobur Aliyev · 100 000 naqd · qolgan qarz 500 000" && a.Tur == "nasiya");
        Assert.Contains(audit, a => a.Tafsilot == "Bobur Aliyev · 50 000 plastik · qolgan qarz 450 000");

        // Smena yopilgach: smena hisobiga qaytish yozib bo'lmaydi (ochiq smena yo'q); boshliq smenadan tashqari yoza oladi.
        var aparatlar = await Aparatlar(op);
        await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar), naqd: 0));
        await Kut(HttpStatusCode.Conflict, await Qaytar(op, n2.Id, 10_000));
        await Oqi<NasiyaDto>(await Qaytar(bosh, n2.Id, 10_000, smenaga: false));
    }

    [Fact]
    public async Task Ochirish_MuallifBoshliqQaytishlarVaSmenaQoidalari()
    {
        var (_, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var smena = await Och(ali);
        var n1 = await Oqi<NasiyaDto>(await Yoz(ali, Yangi("Bir", 100_000)));
        var n2 = await Oqi<NasiyaDto>(await Yoz(vali, Yangi("Ikki", 200_000)));

        await Kut(HttpStatusCode.Forbidden, await vali.DeleteAsync($"/nasiyalar/{n1.Id}"));              // boshqaning nasiyasini operator o'chira olmaydi
        await Kut(HttpStatusCode.NoContent, await ali.DeleteAsync($"/nasiyalar/{n1.Id}"));
        await Kut(HttpStatusCode.NoContent, await bosh.DeleteAsync($"/nasiyalar/{n2.Id}"));              // boshliq - istalganini
        Assert.Equal(0, (await Oqi<SmenaTafsilotDto>(await ali.GetAsync("/smenalar/joriy"))).Smena.NasiyaJami);
        await Kut(HttpStatusCode.NotFound, await ali.DeleteAsync($"/nasiyalar/{n1.Id}"));

        // Qaytishi bor nasiya o'chirilmaydi; avval qaytishni o'chirish kerak, nasiya qoldig'i tiklanadi.
        var n3 = await Oqi<NasiyaDto>(await Yoz(ali, Yangi("Uch", 300_000)));
        await Oqi<NasiyaDto>(await Qaytar(ali, n3.Id, 100_000));
        var q = (await Oqi<NasiyaTafsilotDto>(await ali.GetAsync($"/nasiyalar/{n3.Id}"))).Qaytishlar.Single();
        await Kut(HttpStatusCode.Conflict, await ali.DeleteAsync($"/nasiyalar/{n3.Id}"));
        await Kut(HttpStatusCode.Forbidden, await vali.DeleteAsync($"/nasiyalar/qaytishlar/{q.Id}"));
        await Kut(HttpStatusCode.NoContent, await ali.DeleteAsync($"/nasiyalar/qaytishlar/{q.Id}"));
        Assert.Equal(300_000, (await Oqi<NasiyaTafsilotDto>(await ali.GetAsync($"/nasiyalar/{n3.Id}"))).Nasiya.Qoldiq);
        Assert.Equal(0, (await Oqi<SmenaTafsilotDto>(await ali.GetAsync("/smenalar/joriy"))).Smena.QaytganNasiya);
        await Kut(HttpStatusCode.NotFound, await ali.DeleteAsync($"/nasiyalar/qaytishlar/{q.Id}"));

        // Smenaga bog'lanmagan qaytishni (boshliq yozgan) faqat boshliq o'chira oladi.
        await Oqi<NasiyaDto>(await Qaytar(bosh, n3.Id, 50_000, smenaga: false));
        var boshliqQaytishi = (await Oqi<NasiyaTafsilotDto>(await ali.GetAsync($"/nasiyalar/{n3.Id}"))).Qaytishlar.Single();
        Assert.Null(boshliqQaytishi.SmenaId);
        await Kut(HttpStatusCode.Forbidden, await ali.DeleteAsync($"/nasiyalar/qaytishlar/{boshliqQaytishi.Id}"));
        await Kut(HttpStatusCode.NoContent, await bosh.DeleteAsync($"/nasiyalar/qaytishlar/{boshliqQaytishi.Id}"));

        // Smena yopilgach hech narsa o'chirilmaydi (yig'indilar yopilgan hisobga kirgan).
        await Oqi<NasiyaDto>(await Qaytar(ali, n3.Id, 20_000));
        var yopiq = (await Oqi<NasiyaTafsilotDto>(await ali.GetAsync($"/nasiyalar/{n3.Id}"))).Qaytishlar.Single();
        await Oqi<SmenaDto>(await Yop(ali, smena.Id, Oxirgi(await Aparatlar(ali)), naqd: 0));
        await Kut(HttpStatusCode.Conflict, await bosh.DeleteAsync($"/nasiyalar/{n3.Id}"));
        await Kut(HttpStatusCode.Conflict, await bosh.DeleteAsync($"/nasiyalar/qaytishlar/{yopiq.Id}"));
        var audit = (await (await Admin()).GetFromJsonAsync<AuditYozuviDto[]>("/audit?q=o%27chirildi&limit=50", Json))!;
        Assert.Contains(audit, a => a.Amal == "Nasiya o'chirildi");
        Assert.Contains(audit, a => a.Amal == "Qarz qaytishi o'chirildi");
    }
}
