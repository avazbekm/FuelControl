using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Yopishda plastik bir nechta summa bilan kiritiladi (docs 8.9): SmenaYopishDto.PlastikSummalari va SmenaDto.PlastikSummalari.</summary>
public sealed class SmenaPlastikApiTests : ApiBaza
{
    // Smena: ochishda terminal 200 000; 800 litr AI-92 x 12 200 = 9 760 000; kutilgan naqd = 100 000 + 9 760 000 - (8 280 000 - 200 000) = 1 780 000.
    private const long OchishTerminal = 200_000, Yopish = 8_280_000, Naqd = 1_780_000;

    private async Task<(HttpClient Op, SmenaDto Smena, AparatDto[] Aparatlar)> Ochiq()
    {
        var (_, op) = await Yarat("ali");
        var smena = await Och(op, qaytim: 100_000, terminal: OchishTerminal, depozit: 200_000);
        return (op, smena, await Aparatlar(op));
    }

    [Fact]
    public async Task RoyxatBilanYopish_Saqlanadi_HammaJoydaQaytadi()
    {
        var (op, smena, aparatlar) = await Ochiq();
        long[] qismlar = [7_830_000, 300_000, 150_000];

        var yopilgan = await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, plastik: qismlar));

        Assert.Equal(qismlar, yopilgan.PlastikSummalari);
        Assert.Equal((Yopish, 8_080_000L, 0L), (yopilgan.YopishTerminal, yopilgan.Plastik, yopilgan.Farq));       // formula o'zgarmagan: Plastik = Terminal - OchishTerminal
        Assert.Equal(Naqd, yopilgan.Kutilgan);
        var tafsilot = await Oqi<SmenaTafsilotDto>(await op.GetAsync($"/smenalar/{smena.Id}"));
        Assert.Equal(qismlar, tafsilot.Smena.PlastikSummalari);
        Assert.Equal(qismlar, (await op.GetFromJsonAsync<SmenaDto[]>("/smenalar", Json))!.Single().PlastikSummalari);
        Assert.Equal(qismlar, (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/oxirgi"))).Smena.PlastikSummalari);
    }

    [Fact]
    public async Task OchiqSmenada_PlastikSummalariBosh()
    {
        var (op, smena, _) = await Ochiq();

        Assert.Empty(smena.PlastikSummalari);
        Assert.Empty((await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Smena.PlastikSummalari);
    }

    [Fact]
    public async Task YigindiTerminalgaTengEmas_400_AniqXabar_SmenaOchiqQoladi_KeyinTogriYopiladi()
    {
        var (op, smena, aparatlar) = await Ochiq();

        var javob = await Yop(op, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, plastik: [7_830_000, 300_000]);

        await Kut(HttpStatusCode.BadRequest, javob);
        Assert.Equal("Plastik summalari yig'indisi (8 130 000) terminalga (8 280 000) teng bo'lishi kerak.", await Detail(javob));
        Assert.Null((await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"))).Smena.Tugadi);              // smena ochiq qoldi
        var keyin = await Aparatlar(op);
        Assert.Equal(aparatlar.Select(a => (a.TotalLitr, a.BakQoldiq)), keyin.Select(a => (a.TotalLitr, a.BakQoldiq)));   // pult ham, bak ham tegilmagan

        var yopilgan = await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, plastik: [7_830_000, 450_000]));
        Assert.Equal([7_830_000L, 450_000L], yopilgan.PlastikSummalari);
    }

    [Fact]
    public async Task ManfiySumma_Yoki21TaElement_400_Aynan20TaMumkin()
    {
        var (op, smena, aparatlar) = await Ochiq();

        var manfiy = await Yop(op, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, plastik: [Yopish + 5, -5]);
        await Kut(HttpStatusCode.BadRequest, manfiy);
        Assert.Equal("Plastik summasi manfiy bo'lishi mumkin emas.", await Detail(manfiy));

        var kop = await Yop(op, smena.Id, Oxirgi(aparatlar, 800m), terminal: 0, depozit: 200_000, naqd: Naqd, plastik: Enumerable.Repeat(0L, 21).ToArray());
        await Kut(HttpStatusCode.BadRequest, kop);
        Assert.Equal("Plastik summalari ko'pi bilan 20 ta bo'lishi mumkin (21 ta berilgan).", await Detail(kop));

        var yigirma = await Yop(op, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, plastik: Enumerable.Repeat(Yopish / 20, 20).ToArray());
        Assert.Equal(20, (await Oqi<SmenaDto>(yigirma)).PlastikSummalari.Length);
    }

    [Fact]
    public async Task RoyxatsizYopish_EskiKlient_OzgarishsizIshlaydi_BoshMassiv()
    {
        var (op, smena, aparatlar) = await Ochiq();
        // Eski klient "plastikSummalari" maydonini umuman yubormaydi.
        var tana = JsonSerializer.Serialize(new { korsatkichlar = Oxirgi(aparatlar, 800m), terminal = Yopish, depozit = 200_000L, sanalganNaqd = Naqd, izoh = (string?)null }, Json);

        var yopilgan = await Oqi<SmenaDto>(await op.PostAsync($"/smenalar/{smena.Id}/yop", new StringContent(tana, Encoding.UTF8, "application/json")));

        Assert.Empty(yopilgan.PlastikSummalari);
        Assert.Equal((Yopish, 8_080_000L, 0L), (yopilgan.YopishTerminal, yopilgan.Plastik, yopilgan.Farq));
        Assert.Empty((await Oqi<SmenaTafsilotDto>(await op.GetAsync($"/smenalar/{smena.Id}"))).Smena.PlastikSummalari);
    }

    [Fact]
    public async Task PlastikSummalariNull_EskiXattiHarakat()
    {
        var (op, smena, aparatlar) = await Ochiq();

        var yopilgan = await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, plastik: null));

        Assert.Empty(yopilgan.PlastikSummalari);
        Assert.Equal(Yopish, yopilgan.YopishTerminal);
    }

    [Fact]
    public async Task KorsatkichTuzatish_PlastikQismlariniOzgartirmaydi()
    {
        var admin = await Admin();
        var smena = await Och(admin, qaytim: 100_000, terminal: OchishTerminal, depozit: 200_000);
        var aparatlar = await Aparatlar(admin);
        await Oqi<SmenaDto>(await Yop(admin, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, plastik: [8_000_000, 280_000]));

        var javob = await admin.PutAsJsonAsync($"/smenalar/{smena.Id}/korsatkich", new KorsatkichTuzatishDto(aparatlar[0].Id, 810m, "Xato o'qilgan"), Json);

        var tafsilot = await Oqi<SmenaTafsilotDto>(javob);
        Assert.Equal([8_000_000L, 280_000L], tafsilot.Smena.PlastikSummalari);
        Assert.Equal(Yopish, tafsilot.Smena.YopishTerminal);
    }
}

/// <summary>Demo (Development) ma'lumotidagi hamma smena "eski": plastik qismlari yo'q, SmenaDto.PlastikSummalari = [].</summary>
public sealed class SmenaPlastikEskiSmenalarTests : ApiBaza
{
    protected override string Muhit => "Development";
    protected override bool DemoSozlamasi => true;

    [Fact]
    public async Task EskiVaOchiqSmenalarda_PlastikSummalariBoshMassiv()
    {
        var admin = await Admin();

        var hammasi = (await admin.GetFromJsonAsync<SmenaDto[]>("/smenalar", Json))!;

        Assert.True(hammasi.Length >= 10);
        Assert.Contains(hammasi, s => s.Tugadi is null);                                   // ochiq #42
        Assert.All(hammasi, s => Assert.NotNull(s.PlastikSummalari));
        Assert.All(hammasi, s => Assert.Empty(s.PlastikSummalari));
        var yopiq = hammasi.First(s => s.Tugadi is not null);
        Assert.Empty((await Oqi<SmenaTafsilotDto>(await admin.GetAsync($"/smenalar/{yopiq.Id}"))).Smena.PlastikSummalari);
    }
}
