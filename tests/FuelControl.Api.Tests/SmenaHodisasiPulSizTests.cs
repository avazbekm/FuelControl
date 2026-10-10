using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>
/// Yopilgan smena hodisasi (SmenaOzgardi: yopish va ko'rsatkich tuzatish): to'liq natija faqat "Smenalar" ruxsatlilarga va smena egasiga,
/// qolgan kuzatuvchilarga (keyingi operator) pul maydonlari nollangan nusxa (docs 8.10). Ochiq smena hodisalari o'zgarmaydi.
/// </summary>
public sealed class SmenaHodisasiPulSizTests : ApiBaza
{
    private const long Yopish = 8_280_000, Naqd = 1_780_000;                  // 800 l x 12 200 = 9 760 000; ochishda terminal 200 000, qaytim 100 000

    private sealed class Yigim
    {
        private readonly List<SmenaDto> _smena = [];
        public void Qosh(SmenaDto d) { lock (this) _smena.Add(d); }
        public List<SmenaDto> Nusxa() { lock (this) return [.. _smena]; }
    }

    private async Task<(HubConnection Hub, Yigim Yigim)> Tingla(HttpClient mijoz)
    {
        var y = new Yigim();
        var hub = await HubUlan(mijoz, h => h.On<SmenaDto>("SmenaOzgardi", y.Qosh));
        return (hub, y);
    }

    private static async Task Kutish(Func<bool> shart)
    {
        for (var i = 0; i < 80 && !shart(); i++) await Task.Delay(100);
        Assert.True(shart(), "kutilgan SignalR hodisasi kelmadi");
    }

    private static void ToliqNatija(SmenaDto d, long savdo)
    {
        Assert.NotNull(d.Tugadi);
        Assert.Equal((savdo, 8_080_000L, Yopish, 200_000L, 100_000L), (d.Savdo, d.Plastik, d.YopishTerminal, d.OchishTerminal, d.OchishQaytim));
        Assert.Equal(new[] { 7_830_000L, 300_000, 150_000 }, d.PlastikSummalari);
        Assert.Equal("tinch", d.Izoh);
        Assert.NotNull(d.SanalganNaqd);
    }

    private static void PulSiz(SmenaDto d, SmenaDto toliq, int egasiId)
    {
        var kutilgan = new SmenaDto(toliq.Id, egasiId, "ali", toliq.Boshlandi, toliq.Tugadi, 0, 0, 0, null, null, null, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, []);
        Assert.Empty(d.PlastikSummalari);
        Assert.Equal(kutilgan with { PlastikSummalari = d.PlastikSummalari }, d);
    }

    [Fact]
    public async Task YopilganSmena_ToliqFaqatBoshliqlarVaEgaga_QolganKuzatuvchilargaPulSiz_OchiqSmenaOzgarmaydi()
    {
        var (aliDto, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");                                         // keyingi operator (kuzatuvchi, "Smenalar" yo'q)
        var (_, boshliq) = await Yarat("boshliq", Rol.Boshliq);
        var (_, nazoratchi) = await Yarat("nazoratchi", ruxsatlar: [Ruxsat.Smenalar]);
        var (_, mehmon) = await Yarat("mehmon", ruxsatlar: []);                      // kuzatuvchi ham emas
        var admin = await Admin();
        var ulanishlar = new Dictionary<string, (HubConnection Hub, Yigim Yigim)>();
        foreach (var (nom, mijoz) in new[] { ("ali", ali), ("vali", vali), ("boshliq", boshliq), ("nazoratchi", nazoratchi), ("mehmon", mehmon), ("admin", admin) })
            ulanishlar[nom] = await Tingla(mijoz);
        try
        {
            // 1) Ochiq smena: hodisa o'zgarmagan - kuzatuvchilar (valini ham) ochish qoldiqlari bilan oladi.
            var smena = await Och(ali, qaytim: 100_000, terminal: 200_000, depozit: 200_000);
            foreach (var nom in new[] { "ali", "vali", "boshliq", "nazoratchi", "admin" }) await Kutish(() => ulanishlar[nom].Yigim.Nusxa().Count >= 1);
            var ochiqVali = ulanishlar["vali"].Yigim.Nusxa()[0];
            Assert.Equal((smena.Id, (DateTime?)null, 100_000L, 200_000L), (ochiqVali.Id, ochiqVali.Tugadi, ochiqVali.OchishQaytim, ochiqVali.OchishTerminal));

            // 2) Yopish: to'liq DTO - ega va Smenalar ruxsatlilarga; vali pul maydonlari nollangan nusxani oladi.
            var yopilgan = await Oqi<SmenaDto>(await Yop(ali, smena.Id, Oxirgi(await Aparatlar(ali), 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, izoh: "tinch",
                plastik: [7_830_000, 300_000, 150_000]));
            foreach (var nom in new[] { "ali", "vali", "boshliq", "nazoratchi", "admin" }) await Kutish(() => ulanishlar[nom].Yigim.Nusxa().Count >= 2);
            foreach (var nom in new[] { "ali", "boshliq", "nazoratchi", "admin" }) ToliqNatija(ulanishlar[nom].Yigim.Nusxa()[^1], 9_760_000);
            PulSiz(ulanishlar["vali"].Yigim.Nusxa()[^1], yopilgan, aliDto.Id);

            // 3) Ko'rsatkich tuzatish ham xuddi shunday: 810 l -> savdo 9 882 000.
            var aparat = (await Aparatlar(admin))[0];
            await Oqi<SmenaTafsilotDto>(await admin.PutAsJsonAsync($"/smenalar/{smena.Id}/korsatkich", new KorsatkichTuzatishDto(aparat.Id, 810m, "Xato o'qilgan"), Json));
            foreach (var nom in new[] { "ali", "vali", "boshliq", "nazoratchi", "admin" }) await Kutish(() => ulanishlar[nom].Yigim.Nusxa().Count >= 3);
            foreach (var nom in new[] { "ali", "boshliq", "nazoratchi", "admin" }) Assert.Equal(9_882_000, ulanishlar[nom].Yigim.Nusxa()[^1].Savdo);
            PulSiz(ulanishlar["vali"].Yigim.Nusxa()[^1], yopilgan, aliDto.Id);

            // Vali yopilgan smenaning hech bir hodisasida natija olmadi; ruxsatsiz ulanish hech narsa olmadi.
            Assert.All(ulanishlar["vali"].Yigim.Nusxa().Where(s => s.Tugadi is not null), s => Assert.Equal((0L, 0L, 0L), (s.Savdo, s.Plastik, s.Farq)));
            Assert.Empty(ulanishlar["mehmon"].Yigim.Nusxa());
        }
        finally
        {
            foreach (var (hub, _) in ulanishlar.Values) await hub.DisposeAsync();
        }
    }

    [Fact]
    public async Task Ega_KuzatuvchiBolmasaHam_OzSmenasiningToliqNatijasiniOladi()
    {
        var (_, yolgiz) = await Yarat("yolgiz", ruxsatlar: [Ruxsat.SmenaOchish, Ruxsat.SmenaYopish]);       // kuzatuvchi guruhida emas
        var (hub, y) = await Tingla(yolgiz);
        try
        {
            var smena = await Och(yolgiz, qaytim: 100_000, terminal: 200_000, depozit: 200_000);
            var aparatlar = await Aparatlar(yolgiz);
            await Oqi<SmenaDto>(await Yop(yolgiz, smena.Id, Oxirgi(aparatlar, 800m), terminal: Yopish, depozit: 200_000, naqd: Naqd, izoh: "tinch", plastik: [7_830_000, 300_000, 150_000]));

            await Kutish(() => y.Nusxa().Count >= 1);
            Assert.Single(y.Nusxa());                                    // ochish hodisasi kuzatuvchilarga edi, yopilgan smena - egasiga ham
            ToliqNatija(y.Nusxa()[0], 9_760_000);
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }
}
