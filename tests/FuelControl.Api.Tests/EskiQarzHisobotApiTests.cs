using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Spec 8.11: HisobotDto.EskiQarzQaytgan / EskiQarzSoni (davr + operator filtri, yopilgan smenalar) va SignalR NasiyaOzgardi.</summary>
public sealed class EskiQarzHisobotApiTests : ApiBaza
{
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private static async Task<HisobotDto> Hisobot(HttpClient m, string sorov = "") => await Oqi<HisobotDto>(await m.GetAsync("/hisobot" + sorov));

    [Fact]
    public async Task Hisobot_EskiQarzQaytganVaSoni_FaqatYopilganSmenalar_DavrVaOperatorFiltri()
    {
        var (ali, aliM) = await Yarat("ali");
        var (vali, _) = await Yarat("vali");
        var admin = await Admin();
        var smena = await Och(aliM);
        var oddiy = await Oqi<NasiyaDto>(await EskiQarzApiTests.OddiyYoz(aliM, 50_000));
        await Kut(HttpStatusCode.OK, await aliM.PostAsJsonAsync($"/nasiyalar/{oddiy.Id}/qaytish", new NasiyaQaytishiYaratishDto(20_000, TolovTuri.Naqd, true, null), Json));
        await EskiQarzApiTests.EskiYoz(aliM, EskiQarzApiTests.Eski(300_000));
        await EskiQarzApiTests.EskiYoz(aliM, EskiQarzApiTests.Eski(120_000, TolovTuri.Plastik, "Hamid"));

        var ochiq = await Hisobot(admin);                                                          // ochiq smena hisobotga kirmaydi
        Assert.Equal((0L, 0), (ochiq.EskiQarzQaytgan, ochiq.EskiQarzSoni));

        await Oqi<SmenaDto>(await Yop(aliM, smena.Id, Oxirgi(await Aparatlar(aliM)), terminal: 50_000 + 120_000, naqd: 0));
        var h = await Hisobot(admin);
        Assert.Equal((420_000L, 2), (h.EskiQarzQaytgan, h.EskiQarzSoni));
        Assert.Equal((50_000L, 440_000L), (h.Jami.Nasiya, h.Jami.QaytganNasiya));                  // Nasiya - faqat berilgani; QaytganNasiya - eski qarz ham, oddiy ham
        Assert.True(h.EskiQarzQaytgan <= h.Jami.QaytganNasiya);

        Assert.Equal((420_000L, 2), Juft(await Hisobot(admin, $"?operatorId={ali.Id}")));          // smena operatori bo'yicha
        Assert.Equal((0L, 0), Juft(await Hisobot(admin, $"?operatorId={vali.Id}")));
        Assert.Equal((420_000L, 2), Juft(await Hisobot(admin, $"?dan={Bugun:yyyy-MM-dd}&gacha={Bugun:yyyy-MM-dd}")));
        Assert.Equal((0L, 0), Juft(await Hisobot(admin, $"?dan={Bugun.AddDays(1):yyyy-MM-dd}")));  // davrdan tashqari
        Assert.Equal((0L, 0), Juft(await Hisobot(admin, $"?gacha={Bugun.AddDays(-1):yyyy-MM-dd}")));
        Assert.Equal((420_000L, 2), Juft(await Hisobot(admin, "?guruh=operator")));               // guruhlash yig'indiga ta'sir qilmaydi
    }

    private static (long Yigindi, int Soni) Juft(HisobotDto h) => (h.EskiQarzQaytgan, h.EskiQarzSoni);

    [Fact]
    public async Task EskiQarz_NasiyaOzgardiHodisasiKuzatuvchilargaKeladi()
    {
        var (_, ali) = await Yarat("ali");
        await Och(ali);
        var admin = await Admin();
        var kelgan = new List<NasiyaDto>();
        var hub = await HubUlan(admin, h => h.On<NasiyaDto>("NasiyaOzgardi", d => { lock (kelgan) kelgan.Add(d); }));
        try
        {
            var t = await Oqi<NasiyaTafsilotDto>(await EskiQarzApiTests.EskiYoz(ali, EskiQarzApiTests.Eski()));
            for (var i = 0; i < 80 && Soni(kelgan) == 0; i++) await Task.Delay(100);

            Assert.Equal(1, Soni(kelgan));
            lock (kelgan) Assert.Equal((t.Nasiya.Id, true, "Bobur Aliyev", 0L), (kelgan[0].Id, kelgan[0].EskiQarz, kelgan[0].MijozIsmi, kelgan[0].Qoldiq));
        }
        finally { await hub.DisposeAsync(); }
    }

    private static int Soni(List<NasiyaDto> royxat) { lock (royxat) return royxat.Count; }
}
