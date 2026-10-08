using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>SignalR hodisalari (SmenaOzgardi, NasiyaOzgardi, XarajatOzgardi, AparatOzgardi, NarxOzgardi): yuk - o'zgargan yozuv DTO'si; faqat ko'rish ruxsati borlarga.</summary>
public sealed class HodisaApiTests : ApiBaza
{
    private sealed class Yigim
    {
        public readonly List<SmenaDto> Smena = [];
        public readonly List<NasiyaDto> Nasiya = [];
        public readonly List<XarajatDto> Xarajat = [];
        public readonly List<AparatDto> Aparat = [];
        public readonly List<YoqilgiTuriDto> Narx = [];
        public int Soni(Func<Yigim, int> f) { lock (this) return f(this); }
    }

    private async Task<(HubConnection Hub, Yigim Yigim)> Tingla(HttpClient mijoz)
    {
        var y = new Yigim();
        var hub = await HubUlan(mijoz, h =>
        {
            h.On<SmenaDto>("SmenaOzgardi", d => { lock (y) y.Smena.Add(d); });
            h.On<NasiyaDto>("NasiyaOzgardi", d => { lock (y) y.Nasiya.Add(d); });
            h.On<XarajatDto>("XarajatOzgardi", d => { lock (y) y.Xarajat.Add(d); });
            h.On<AparatDto>("AparatOzgardi", d => { lock (y) y.Aparat.Add(d); });
            h.On<YoqilgiTuriDto>("NarxOzgardi", d => { lock (y) y.Narx.Add(d); });
        });
        return (hub, y);
    }

    private static async Task Kutish(Func<bool> shart)
    {
        for (var i = 0; i < 80 && !shart(); i++) await Task.Delay(100);
        Assert.True(shart(), "kutilgan SignalR hodisasi kelmadi");
    }

    [Fact]
    public async Task Hodisalar_KoruvchilargaKeladi_RuxsatsizlargaKelmaydi_OchirishdaHamKeladi()
    {
        var (_, op) = await Yarat("ali");
        var (_, mehmon) = await Yarat("mehmon", ruxsatlar: []);                  // hech qanday ko'rish ruxsati yo'q
        var admin = await Admin();
        var (hubAdmin, boshliq) = await Tingla(admin);
        var (hubMehmon, tashqari) = await Tingla(mehmon);
        var (hubOp, operatorlar) = await Tingla(op);
        try
        {
            var smena = await Och(op);
            var nasiya = await Oqi<NasiyaDto>(await op.PostAsJsonAsync("/nasiyalar",
                new NasiyaYaratishDto("Bobur", "+998901112233", "", 100_000, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(3), null), Json));
            await Oqi<XarajatDto>(await op.PostAsJsonAsync("/xarajatlar", new XarajatYaratishDto(5_000, "Sabab", XarajatManbai.Kassa), Json));
            var aparatlar = await Aparatlar(admin);
            await Oqi<AparatDto>(await admin.PostAsJsonAsync($"/aparatlar/{aparatlar[0].Id}/kirim", new BakKirimYaratishDto(100m, null, null), Json));

            await Kutish(() => boshliq.Soni(y => Math.Min(Math.Min(y.Smena.Count, y.Nasiya.Count), Math.Min(y.Xarajat.Count, y.Aparat.Count))) >= 1);
            Assert.Equal(smena.Id, boshliq.Smena[0].Id);
            Assert.Equal(("Bobur", nasiya.Id), (boshliq.Nasiya[0].MijozIsmi, boshliq.Nasiya[0].Id));
            Assert.Equal(5_000, boshliq.Xarajat[0].Summa);
            Assert.Equal(100m, boshliq.Aparat[0].BakQoldiq);
            await Kutish(() => operatorlar.Soni(y => y.Nasiya.Count) >= 1);                     // operator (Savdo/Nasiyalar) ham ko'radi

            // Yopish: SmenaOzgardi + har aparat uchun AparatOzgardi (yangi pult va bak).
            await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(await Aparatlar(op), 10m), naqd: 0));
            await Kutish(() => boshliq.Soni(y => y.Smena.Count) >= 2 && boshliq.Soni(y => y.Aparat.Count) >= 1 + 5);
            Assert.NotNull(boshliq.Smena[^1].Tugadi);

            // O'chirish ham hodisa beradi (klient ro'yxatni qayta yuklaydi): ochiq smena kerak - yangisini ochamiz.
            await Och(op);
            var ikkinchi = await Oqi<NasiyaDto>(await op.PostAsJsonAsync("/nasiyalar",
                new NasiyaYaratishDto("Sherzod", "+998902223344", "", 50_000, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(3), null), Json));
            var oldin = boshliq.Soni(y => y.Nasiya.Count);
            await Kut(HttpStatusCode.NoContent, await op.DeleteAsync($"/nasiyalar/{ikkinchi.Id}"));
            await Kutish(() => boshliq.Soni(y => y.Nasiya.Count) >= oldin + 1);

            // Narx o'zgarishi hammaga (NarxOzgardi - YoqilgiTuriDto), ruxsatsiz ulanishga ham.
            var yoqilgi = (await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))!.Single(y => y.Nomi == "Dizel");
            var smenaYopilgan = (await admin.GetFromJsonAsync<SmenaDto[]>("/smenalar", Json))!.First(s => s.Tugadi is null);
            await Oqi<SmenaDto>(await Yop(op, smenaYopilgan.Id, Oxirgi(await Aparatlar(op)), naqd: 0));
            await Oqi<YoqilgiTuriDto>(await admin.PutAsJsonAsync($"/yoqilgilar/{yoqilgi.Id}", new YoqilgiTahrirlashDto("Dizel", 14_000, yoqilgi.Rang), Json));
            await Kutish(() => tashqari.Soni(y => y.Narx.Count) >= 1 && boshliq.Soni(y => y.Narx.Count) >= 1);
            Assert.Equal(14_000, tashqari.Narx[0].Narx);

            // Ruxsatsiz ulanish smena/nasiya/xarajat/aparat hodisalarini olmaydi.
            Assert.Equal((0, 0, 0, 0), (tashqari.Soni(y => y.Smena.Count), tashqari.Soni(y => y.Nasiya.Count), tashqari.Soni(y => y.Xarajat.Count), tashqari.Soni(y => y.Aparat.Count)));
        }
        finally
        {
            await hubAdmin.DisposeAsync();
            await hubMehmon.DisposeAsync();
            await hubOp.DisposeAsync();
        }
    }
}
