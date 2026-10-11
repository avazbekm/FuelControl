using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Spec 8.11: eski qarz to'lovi nasiyasi va qaytishi bir juftlik - qaysi tomondan o'chirilsa ham ikkalasi ketadi; mavjud o'chirish qoidalari amal qiladi.</summary>
public sealed class EskiQarzOchirishApiTests : ApiBaza
{
    private static Task<HttpResponseMessage> EskiYoz(HttpClient m, EskiQarzYaratishDto d) => EskiQarzApiTests.EskiYoz(m, d);
    private static Task<SmenaTafsilotDto> Joriy(HttpClient m) => EskiQarzApiTests.Joriy(m);

    [Fact]
    public async Task QaytishniOchirish_NasiyasiHamKetadi_BoshqaOperatorOchiraOlmaydi_BoshliqOchiradi()
    {
        var (_, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var (_, boshliq) = await Yarat("boshliq", Rol.Boshliq);
        var admin = await Admin();
        var smena = await Och(ali);
        var t = await Oqi<NasiyaTafsilotDto>(await EskiYoz(ali, EskiQarzApiTests.Eski()));
        var qaytishId = t.Qaytishlar[0].Id;

        await Kut(HttpStatusCode.Forbidden, await vali.DeleteAsync($"/nasiyalar/qaytishlar/{qaytishId}"));          // muallif ham, boshliq ham emas
        Assert.Equal(300_000L, (await Joriy(ali)).Smena.QaytganNasiya);
        await Kut(HttpStatusCode.NoContent, await boshliq.DeleteAsync($"/nasiyalar/qaytishlar/{qaytishId}"));

        await Kut(HttpStatusCode.NotFound, await admin.GetAsync($"/nasiyalar/{t.Nasiya.Id}"));                     // nasiya ham o'chdi
        var joriy = await Joriy(ali);
        Assert.Equal((0L, 0L), (joriy.Smena.NasiyaJami, joriy.Smena.QaytganNasiya));
        Assert.Empty(joriy.Qaytishlar);
        Assert.Empty((await admin.GetFromJsonAsync<NasiyalarDto>("/nasiyalar", Json))!.Royxat);

        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=nasiya", Json))!;
        var yozuv = Assert.Single(audit, a => a.Amal == "Eski qarz o'chirildi");                                   // bitta amal - bitta yozuv
        Assert.Equal($"Bobur Aliyev · 300 000 naqd · smena #{smena.Id} · nasiya yozuvi ham o'chirildi", yozuv.Tafsilot);
        Assert.DoesNotContain(audit, a => a.Amal is "Qarz qaytishi o'chirildi" or "Nasiya o'chirildi");
    }

    [Fact]
    public async Task NasiyaniOchirish_EskiQarzdaQaytishiBilanKetadi_OddiyNasiyadaQaytishiBorBolsa409()
    {
        var (_, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var admin = await Admin();
        await Och(ali);
        var eski = await Oqi<NasiyaTafsilotDto>(await EskiYoz(ali, EskiQarzApiTests.Eski(120_000, TolovTuri.Plastik, "Hamid")));

        await Kut(HttpStatusCode.Forbidden, await vali.DeleteAsync($"/nasiyalar/{eski.Nasiya.Id}"));
        await Kut(HttpStatusCode.NoContent, await ali.DeleteAsync($"/nasiyalar/{eski.Nasiya.Id}"));                // 409 emas: juftlik birga ketadi
        var joriy = await Joriy(ali);
        Assert.Empty(joriy.Qaytishlar);
        Assert.Equal((0L, 0L), (joriy.Smena.NasiyaJami, joriy.Smena.QaytganNasiya));
        Assert.Empty((await admin.GetFromJsonAsync<NasiyalarDto>("/nasiyalar", Json))!.Royxat);
        Assert.Contains((await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=nasiya", Json))!,
            a => a.Amal == "Eski qarz o'chirildi" && a.Tafsilot.StartsWith("Hamid · 120 000 plastik"));

        // Oddiy nasiya: qaytishi bor bo'lsa avvalgidek 409.
        var oddiy = await Oqi<NasiyaDto>(await EskiQarzApiTests.OddiyYoz(ali, 80_000));
        await Kut(HttpStatusCode.OK, await ali.PostAsJsonAsync($"/nasiyalar/{oddiy.Id}/qaytish", new NasiyaQaytishiYaratishDto(10_000, TolovTuri.Naqd, true, null), Json));
        await Kut(HttpStatusCode.Conflict, await ali.DeleteAsync($"/nasiyalar/{oddiy.Id}"));
    }

    [Fact]
    public async Task YopilganSmenadaEskiQarzniOchirib_Bolmaydi_409()
    {
        var (_, ali) = await Yarat("ali");
        var (_, boshliq) = await Yarat("boshliq", Rol.Boshliq);
        var smena = await Och(ali);
        var t = await Oqi<NasiyaTafsilotDto>(await EskiYoz(ali, EskiQarzApiTests.Eski(70_000)));
        await Oqi<SmenaDto>(await Yop(ali, smena.Id, Oxirgi(await Aparatlar(ali)), naqd: 0));

        await Kut(HttpStatusCode.Conflict, await ali.DeleteAsync($"/nasiyalar/{t.Nasiya.Id}"));
        await Kut(HttpStatusCode.Conflict, await boshliq.DeleteAsync($"/nasiyalar/qaytishlar/{t.Qaytishlar[0].Id}"));
        var admin = await Admin();
        Assert.True((await Oqi<NasiyaTafsilotDto>(await admin.GetAsync($"/nasiyalar/{t.Nasiya.Id}"))).Nasiya.EskiQarz);   // joyida qoldi
    }
}
