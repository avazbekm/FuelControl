using System.Net;
using System.Text.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Savdo, smena yopiq: keyingi operator oldingi operatorning pul natijasini ko'rmaydi (docs 8.10).</summary>
public sealed class OxirgiSmenaKorinishTests : ApiBaza
{
    private async Task<SmenaDto> Yopgan(HttpClient op, decimal litr, long depozit)
    {
        var smena = await Och(op);
        return await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(await Aparatlar(op), litr), terminal: 50_000, depozit: depozit, naqd: 0));
    }

    [Fact]
    public async Task Oxirgi_BoshqaOperator403_OzSmenasi200_BoshliqVaSmenalarRuxsatiBorlar200()
    {
        var (_, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var (_, boshliq) = await Yarat("boshliq", Rol.Boshliq);
        var (_, nazoratchi) = await Yarat("nazoratchi", ruxsatlar: [Ruxsat.Smenalar]);        // operator, lekin "Smenalar" ruxsati bor
        var (_, mehmon) = await Yarat("mehmon", ruxsatlar: []);
        var admin = await Admin();
        var yopilgan = await Yopgan(ali, 100m, 1_250_000);

        var rad = await vali.GetAsync("/smenalar/oxirgi");
        await Kut(HttpStatusCode.Forbidden, rad);
        Assert.Equal("Oxirgi smena natijasini faqat boshliq (\"Smenalar\" ruxsati) yoki shu smenaning operatori ko'ra oladi.", await Detail(rad));
        await Kut(HttpStatusCode.Forbidden, await mehmon.GetAsync("/smenalar/oxirgi"));

        foreach (var mijoz in new[] { ali, boshliq, nazoratchi, admin })
        {
            var tafsilot = await Oqi<SmenaTafsilotDto>(await mijoz.GetAsync("/smenalar/oxirgi"));
            Assert.Equal((yopilgan.Id, yopilgan.Savdo), (tafsilot.Smena.Id, tafsilot.Smena.Savdo));      // to'liq natija
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ilova.CreateClient().GetAsync("/smenalar/oxirgi")).StatusCode);
    }

    [Fact]
    public async Task Topshirish_HarKimgaIdOperatorIdIsmVaqtDepozit_PulNatijasiYoq()
    {
        var (aliDto, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var (_, mehmon) = await Yarat("mehmon", ruxsatlar: []);
        var yopilgan = await Yopgan(ali, 100m, 1_250_000);

        var javob = await vali.GetAsync("/smenalar/oxirgi/topshirish");
        var t = await Oqi<SmenaTopshirishDto>(javob);

        Assert.Equal((yopilgan.Id, aliDto.Id, "ali", 1_250_000L), (t.Id, t.OperatorId, t.OperatorIsmi, t.YopishDepozit));
        Assert.True(Math.Abs((t.Tugadi - yopilgan.Tugadi!.Value).TotalSeconds) < 1);
        Assert.Equal(DateTimeKind.Utc, t.Tugadi.Kind);
        // Xom JSON'da aynan shu 5 maydon: savdo, plastik, terminal, kamomat kabi pul natijasi sizib chiqmaydi.
        using var hujjat = JsonDocument.Parse(await javob.Content.ReadAsStringAsync());
        Assert.Equal(["id", "operatorId", "operatorIsmi", "tugadi", "yopishDepozit"], hujjat.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal(yopilgan.Id, (await Oqi<SmenaTopshirishDto>(await mehmon.GetAsync("/smenalar/oxirgi/topshirish"))).Id);   // ruxsati joriy bilan bir xil
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ilova.CreateClient().GetAsync("/smenalar/oxirgi/topshirish")).StatusCode);
    }

    [Fact]
    public async Task OperatorIdMen_BolsaOxirgi200_HechQachon403Emas_KeyingiSmenaAlmashtiradi()
    {
        var (aliDto, ali) = await Yarat("ali");
        var (valiDto, vali) = await Yarat("vali");
        await Yopgan(ali, 100m, 1_250_000);
        await Yopgan(vali, 50m, 900_000);                      // keyingi operator o'z smenasini yopdi: endi "oxirgi" - valiniki

        var t = await Oqi<SmenaTopshirishDto>(await ali.GetAsync("/smenalar/oxirgi/topshirish"));

        Assert.Equal((valiDto.Id, "vali", 900_000L), (t.OperatorId, t.OperatorIsmi, t.YopishDepozit));
        Assert.NotEqual(aliDto.Id, t.OperatorId);
        await Kut(HttpStatusCode.Forbidden, await ali.GetAsync("/smenalar/oxirgi"));              // ali endi boshqaning smenasini ko'ra olmaydi
        Assert.Equal(t.Id, (await Oqi<SmenaTafsilotDto>(await vali.GetAsync("/smenalar/oxirgi"))).Smena.Id);   // OperatorId == men -> 200
    }

    [Fact]
    public async Task YopilganSmenaYoq_204_HammaUchun_OchiqSmenaOxirgiEmas()
    {
        var (_, ali) = await Yarat("ali");
        var (_, mehmon) = await Yarat("mehmon", ruxsatlar: []);
        var admin = await Admin();

        foreach (var mijoz in new[] { ali, mehmon, admin })
        {
            await Kut(HttpStatusCode.NoContent, await mijoz.GetAsync("/smenalar/oxirgi"));
            await Kut(HttpStatusCode.NoContent, await mijoz.GetAsync("/smenalar/oxirgi/topshirish"));
        }
        await Och(ali);                                                                            // ochiq smena "oxirgi yopilgan" emas
        await Kut(HttpStatusCode.NoContent, await mehmon.GetAsync("/smenalar/oxirgi"));
        await Kut(HttpStatusCode.NoContent, await mehmon.GetAsync("/smenalar/oxirgi/topshirish"));
    }
}
