using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

public sealed class XarajatApiTests : ApiBaza
{
    private static Task<HttpResponseMessage> Yoz(HttpClient m, long summa, string sabab, XarajatManbai manba = XarajatManbai.Kassa) =>
        m.PostAsJsonAsync("/xarajatlar", new XarajatYaratishDto(summa, sabab, manba), Json);

    [Fact]
    public async Task Yozish_OchiqSmenaMajburiy_KassaVaDepozitManbasi_Audit()
    {
        var (_, op) = await Yarat("ali");
        await Kut(HttpStatusCode.Conflict, await Yoz(op, 1000, "Sabab"));                    // ochiq smena yo'q

        var smena = await Och(op);
        var x1 = await Oqi<XarajatDto>(await Yoz(op, 85_000, "  Lampochka va tozalash vositasi "));
        var x2 = await Oqi<XarajatDto>(await Yoz(op, 1_500_000, "Boshliq naqd oldi", XarajatManbai.Depozit));
        Assert.Equal((smena.Id, 85_000L, "Lampochka va tozalash vositasi", XarajatManbai.Kassa, "ali"), (x1.SmenaId, x1.Summa, x1.Sabab, x1.Manba, x1.KimYozdi));
        Assert.Equal(XarajatManbai.Depozit, x2.Manba);
        Assert.Equal(1_585_000, (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Smena.XarajatJami);   // ikkala manba ham xarajat

        var admin = await Admin();
        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=xarajat", Json))!;
        Assert.Contains(audit, a => a.Amal == "Xarajat yozildi" && a.Tafsilot == $"Lampochka va tozalash vositasi · 85 000 · kassadan · smena #{smena.Id}" && a.Kim == "ali");
        Assert.Contains(audit, a => a.Tafsilot == $"Boshliq naqd oldi · 1 500 000 · depozitdan · smena #{smena.Id}");
    }

    [Fact]
    public async Task Yozish_Validatsiya_VaRuxsat()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, 0, "Sabab"));
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, -5, "Sabab"));
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, 1000, "  "));
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, 1000, new string('x', 201)));
        var notogri = await op.PostAsJsonAsync("/xarajatlar", new { summa = 1000, sabab = "S", manba = "Naqd" }, Json);   // manba faqat Kassa/Depozit
        await Kut(HttpStatusCode.BadRequest, notogri);
        var (_, ruxsatsiz) = await Yarat("sardor", ruxsatlar: [Ruxsat.Savdo]);                                     // XarajatYozish yo'q
        await Kut(HttpStatusCode.Forbidden, await Yoz(ruxsatsiz, 1000, "Sabab"));
    }

    [Fact]
    public async Task Royxat_FiltrlarVaKorishRuxsati()
    {
        var (_, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var smena = await Och(ali);
        await Oqi<XarajatDto>(await Yoz(ali, 1000, "Bir"));
        await Oqi<XarajatDto>(await Yoz(vali, 2000, "Ikki"));        // boshqa operator ham ochiq smenaga yoza oladi

        Assert.Equal(2, (await bosh.GetFromJsonAsync<XarajatDto[]>($"/xarajatlar?smenaId={smena.Id}", Json))!.Length);
        Assert.Equal(["Ikki", "Bir"], (await bosh.GetFromJsonAsync<XarajatDto[]>("/xarajatlar", Json))!.Select(x => x.Sabab));      // yangisi tepada
        Assert.Equal(2, (await ali.GetFromJsonAsync<XarajatDto[]>("/xarajatlar", Json))!.Length);          // ali smenani ochgan: smena xarajatlarini ko'radi
        Assert.Empty((await vali.GetFromJsonAsync<XarajatDto[]>("/xarajatlar", Json))!);                    // vali ochmagan va "Smenalar" ruxsati yo'q
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        Assert.Equal(2, (await bosh.GetFromJsonAsync<XarajatDto[]>($"/xarajatlar?dan={bugun:yyyy-MM-dd}&gacha={bugun:yyyy-MM-dd}", Json))!.Length);
        Assert.Empty((await bosh.GetFromJsonAsync<XarajatDto[]>($"/xarajatlar?dan={bugun.AddDays(1):yyyy-MM-dd}", Json))!);
    }

    [Fact]
    public async Task Ochirish_MuallifBoshliqVaSmenaOchiqligi()
    {
        var (_, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var smena = await Och(ali);
        var x1 = await Oqi<XarajatDto>(await Yoz(ali, 1000, "Bir"));
        var x2 = await Oqi<XarajatDto>(await Yoz(vali, 2000, "Ikki"));
        var x3 = await Oqi<XarajatDto>(await Yoz(ali, 4000, "Uch"));

        await Kut(HttpStatusCode.Forbidden, await vali.DeleteAsync($"/xarajatlar/{x1.Id}"));              // boshqaning xarajati
        await Kut(HttpStatusCode.NoContent, await ali.DeleteAsync($"/xarajatlar/{x1.Id}"));
        await Kut(HttpStatusCode.NoContent, await bosh.DeleteAsync($"/xarajatlar/{x2.Id}"));              // boshliq - istalganini
        await Kut(HttpStatusCode.NotFound, await ali.DeleteAsync($"/xarajatlar/{x1.Id}"));
        Assert.Equal(4000, (await Oqi<SmenaTafsilotDto>(await ali.GetAsync("/smenalar/joriy"))).Smena.XarajatJami);

        await Oqi<SmenaDto>(await Yop(ali, smena.Id, Oxirgi(await Aparatlar(ali)), naqd: 0));
        await Kut(HttpStatusCode.Conflict, await bosh.DeleteAsync($"/xarajatlar/{x3.Id}"));               // smena yopilgan
        var admin = await Admin();
        Assert.Contains((await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?q=Xarajat%20o%27chirildi", Json))!, a => a.Amal == "Xarajat o'chirildi");
    }
}
