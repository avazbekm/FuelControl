using System.Net;
using System.Net.Http.Json;
using System.Text;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Spec 8.11: eski qarz to'lovi (POST /nasiyalar/eski-qarz) - nasiya + qaytish bitta tranzaksiyada, smena hisobi, validatsiya, ruxsat.</summary>
public sealed class EskiQarzApiTests : ApiBaza
{
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    internal static EskiQarzYaratishDto Eski(long summa = 300_000, TolovTuri usul = TolovTuri.Naqd, string ism = "Bobur Aliyev", string tel = "97 700 80 90",
        string raqam = "01 H 202 MA") => new(ism, tel, raqam, summa, usul, "eski qarz");

    internal static Task<HttpResponseMessage> EskiYoz(HttpClient m, EskiQarzYaratishDto d) => m.PostAsJsonAsync("/nasiyalar/eski-qarz", d, Json);

    internal static Task<HttpResponseMessage> OddiyYoz(HttpClient m, long summa = 50_000, string ism = "Vali") =>
        m.PostAsJsonAsync("/nasiyalar", new NasiyaYaratishDto(ism, "+998 91 000 11 22", "", summa, Bugun.AddDays(5), null), Json);

    internal static Task<SmenaTafsilotDto> Joriy(HttpClient m) => m.GetFromJsonAsync<SmenaTafsilotDto>("/smenalar/joriy", Json)!;

    [Fact]
    public async Task EskiQarz_NasiyaVaQaytishYaratiladi_DtoVaSmenaTafsiloti()
    {
        var (op, mijoz) = await Yarat("ali");
        await Kut(HttpStatusCode.Conflict, await EskiYoz(mijoz, Eski()));                       // ochiq smena majburiy (nasiya yozishdagidek 409)
        var smena = await Och(mijoz);
        var oddiy = await Oqi<NasiyaDto>(await OddiyYoz(mijoz));

        var javob = await EskiYoz(mijoz, Eski());
        Assert.Equal(HttpStatusCode.Created, javob.StatusCode);
        var t = await Oqi<NasiyaTafsilotDto>(javob);

        var n = t.Nasiya;
        Assert.True(n.EskiQarz);
        Assert.Equal((smena.Id, "ali", "Bobur Aliyev", "+998 97 700 80 90", "01 H 202 MA", op.Id), (n.SmenaId, n.OperatorIsmi, n.MijozIsmi, n.Telefon, n.MashinaRaqami, n.MuallifId));
        Assert.Equal((300_000L, 300_000L, 0L, NasiyaHolati.Yopilgan, 0, "eski qarz"), (n.Summa, n.Qaytgan, n.Qoldiq, n.Holati, n.MuddatgachaKun, n.Izoh));
        Assert.Equal(Bugun, n.Muddat);
        Assert.NotNull(n.Yopildi);
        var q = Assert.Single(t.Qaytishlar);
        Assert.True(q.EskiQarz);
        Assert.Equal((n.Id, (int?)smena.Id, 300_000L, TolovTuri.Naqd, "ali", "Bobur Aliyev", op.Id), (q.NasiyaId, q.SmenaId, q.Summa, q.Usul, q.KimYozdi, q.MijozIsmi, q.MuallifId));

        // GET /nasiyalar/{id}: EskiQarz belgisi; smena tafsilotida "Nasiya berildi" ro'yxatida faqat oddiy nasiya, qaytishlarda esa eski qarz belgisi bilan.
        var admin = await Admin();
        var tafsilot = await Oqi<NasiyaTafsilotDto>(await admin.GetAsync($"/nasiyalar/{n.Id}"));
        Assert.True(tafsilot.Nasiya.EskiQarz && tafsilot.Qaytishlar.Single().EskiQarz);
        var joriy = await Joriy(mijoz);
        Assert.Equal((50_000L, 300_000L), (joriy.Smena.NasiyaJami, joriy.Smena.QaytganNasiya));
        Assert.Equal(new[] { oddiy.Id }, joriy.Nasiyalar.Select(x => x.Id));
        Assert.All(joriy.Nasiyalar, x => Assert.False(x.EskiQarz));
        var qaytish = Assert.Single(joriy.Qaytishlar);
        Assert.True(qaytish.EskiQarz);
        Assert.Equal(q.Id, qaytish.Id);
        Assert.Equal(new[] { oddiy.Id }, (await Oqi<SmenaTafsilotDto>(await admin.GetAsync($"/smenalar/{smena.Id}"))).Nasiyalar.Select(x => x.Id));

        // Audit: "Eski qarz to'landi" - ism, summa, usul, smena.
        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=nasiya", Json))!;
        Assert.Contains(audit, a => a.Amal == "Eski qarz to'landi" && a.Tafsilot == $"Bobur Aliyev · 300 000 naqd · smena #{smena.Id}");
    }

    [Theory]
    [InlineData(TolovTuri.Naqd, 0L, 0L, 350_000L)]             // kutilgan naqd: 100 000 + 300 000 eski qarz - 50 000 oddiy nasiya
    [InlineData(TolovTuri.Plastik, 300_000L, 0L, 50_000L)]     // plastik orqali: terminal +300 000, naqd o'zgarmaydi
    [InlineData(TolovTuri.Depozit, 0L, 300_000L, 50_000L)]     // depozit orqali: karta qoldig'i +300 000
    public async Task EskiQarz_SmenaYopilganda_KutilganTogri_NasiyaJamiOzgarmaydi(TolovTuri usul, long terminalOrtdi, long depozitOrtdi, long kutilgan)
    {
        var (_, mijoz) = await Yarat("ali");
        await Och(mijoz, qaytim: 100_000, terminal: 50_000, depozit: 200_000);
        await OddiyYoz(mijoz, 50_000);
        await EskiYoz(mijoz, Eski(300_000, usul));
        var joriy = await Joriy(mijoz);

        var yopilgan = await Oqi<SmenaDto>(await Yop(mijoz, joriy.Smena.Id, Oxirgi(await Aparatlar(mijoz)),
            terminal: 50_000 + terminalOrtdi, depozit: 200_000 + depozitOrtdi, naqd: kutilgan));

        Assert.Equal((50_000L, 300_000L, kutilgan, 0L), (yopilgan.NasiyaJami, yopilgan.QaytganNasiya, yopilgan.Kutilgan, yopilgan.Farq));
        Assert.Equal((terminalOrtdi, depozitOrtdi), (yopilgan.Plastik, yopilgan.DepozitFarqi));
    }

    [Fact]
    public async Task EskiQarz_Validatsiya400_Ruxsat403_IxtiyoriyMaydonlar()
    {
        var (_, mijoz) = await Yarat("ali");
        await Och(mijoz);
        await Kut(HttpStatusCode.BadRequest, await EskiYoz(mijoz, Eski(ism: " ")));
        await Kut(HttpStatusCode.BadRequest, await EskiYoz(mijoz, Eski(summa: 0)));
        await Kut(HttpStatusCode.BadRequest, await EskiYoz(mijoz, Eski(summa: -1)));
        await Kut(HttpStatusCode.BadRequest, await EskiYoz(mijoz, Eski(tel: "90 123 45 6")));              // 8 raqam
        await Kut(HttpStatusCode.BadRequest, await EskiYoz(mijoz, Eski(ism: new string('a', 101))));
        var notogriUsul = new StringContent("""{"mijozIsmi":"Ali","telefon":"","mashinaRaqami":"","summa":1000,"usul":7,"izoh":null}""", Encoding.UTF8, "application/json");
        await Kut(HttpStatusCode.BadRequest, await mijoz.PostAsync("/nasiyalar/eski-qarz", notogriUsul));  // aniqlanmagan to'lov usuli

        await Kut(HttpStatusCode.Created, await EskiYoz(mijoz, Eski(tel: "", raqam: "")));                  // telefon va mashina raqami ixtiyoriy
        var faqatIsm = new StringContent("""{"mijozIsmi":"Hamid","summa":1000,"usul":"Plastik"}""", Encoding.UTF8, "application/json");
        var t = await Oqi<NasiyaTafsilotDto>(await mijoz.PostAsync("/nasiyalar/eski-qarz", faqatIsm));    // maydonlar umuman yuborilmagan
        Assert.Equal(("Hamid", "", "", TolovTuri.Plastik), (t.Nasiya.MijozIsmi, t.Nasiya.Telefon, t.Nasiya.MashinaRaqami, t.Qaytishlar[0].Usul));

        var (_, ruxsatsiz) = await Yarat("sardor", ruxsatlar: [Ruxsat.Savdo, Ruxsat.Nasiyalar, Ruxsat.NasiyaYozish]);   // QarzQaytdi yo'q
        await Kut(HttpStatusCode.Forbidden, await EskiYoz(ruxsatsiz, Eski()));
        var (_, faqatQarz) = await Yarat("vali", ruxsatlar: [Ruxsat.QarzQaytdi]);                                    // faqat QarzQaytdi yetadi
        await Kut(HttpStatusCode.Created, await EskiYoz(faqatQarz, Eski()));
    }

    [Fact]
    public async Task EskiQarz_NasiyaYozishVaQarzQaytdiOddiyYoldaOzgarmagan_MijozTakliflariEskiYozuvniKoradi()
    {
        var (_, mijoz) = await Yarat("ali");
        await Och(mijoz);
        await EskiYoz(mijoz, Eski(ism: "Hamid Karimov", tel: "93 111 22 33", raqam: ""));
        var oddiy = await Oqi<NasiyaDto>(await OddiyYoz(mijoz, 80_000, "Vali"));
        Assert.False(oddiy.EskiQarz);

        // Yopilgan eski qarz nasiyasiga yana qaytish yozib bo'lmaydi (qoldiq 0), yangi eski qarz yozish esa mumkin.
        var eski = (await mijoz.GetFromJsonAsync<NasiyalarDto>("/nasiyalar?holat=yopilgan", Json))!.Royxat.Single(x => x.EskiQarz);
        await Kut(HttpStatusCode.BadRequest, await mijoz.PostAsJsonAsync($"/nasiyalar/{eski.Id}/qaytish", new NasiyaQaytishiYaratishDto(1000, TolovTuri.Naqd, true, null), Json));
        var takliflar = (await mijoz.GetFromJsonAsync<MijozTaklifDto[]>("/nasiyalar/mijozlar?q=hamid", Json))!;
        Assert.Equal(("Hamid Karimov", "+998 93 111 22 33"), (takliflar.Single().MijozIsmi, takliflar.Single().Telefon));
    }
}
