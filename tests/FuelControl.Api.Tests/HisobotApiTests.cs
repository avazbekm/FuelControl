using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Hisobot (smena/kun/oy/operator, aparat/bak jadvali, avans) va Boshqaruv: faqat yopilgan smenalar, server yig'indilari.</summary>
public sealed class HisobotApiTests : ApiBaza
{
    private static string Bugun(int kun = 0) => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(kun).ToString("yyyy-MM-dd");

    /// <summary>
    /// 1-smena (ali): 1-aparat 100 L = 1 220 000, plastik 400 000, depozit +100 000, xarajat 50 000 (1 ta), kamomat 1 000.
    /// 2-smena (vali): 2-aparat 50 L = 610 000, ortiqcha 5 000. Oldin 1-aparat bakiga 500 L kirim va ali'ga 200 000 avans yozilgan.
    /// </summary>
    private async Task<(FoydalanuvchiDto Ali, FoydalanuvchiDto Vali, SmenaDto Birinchi, SmenaDto Ikkinchi)> Tayyorla()
    {
        var (ali, aliMijoz) = await Yarat("ali");
        var (vali, valiMijoz) = await Yarat("vali");
        var admin = await Admin();
        var aparatlar = await Aparatlar(admin);
        await Oqi<AparatDto>(await admin.PostAsJsonAsync($"/aparatlar/{aparatlar[0].Id}/kirim", new BakKirimYaratishDto(500m, null, "yuk xati 1"), Json));
        await Oqi<HisobHarakatiDto>(await admin.PostAsJsonAsync($"/operatorlar/{ali.Id}/harakat", new HarakatYaratishDto(HarakatTuri.Avans, 200_000, "Naqd berildi"), Json));

        var s1 = await Och(aliMijoz, 100_000, 0, 0);
        await Oqi<XarajatDto>(await aliMijoz.PostAsJsonAsync("/xarajatlar", new XarajatYaratishDto(50_000, "Tozalash", XarajatManbai.Kassa), Json));
        var birinchi = await Oqi<SmenaDto>(await Yop(aliMijoz, s1.Id, Oxirgi(aparatlar, 100m), terminal: 400_000, depozit: 100_000, naqd: 769_000));
        var s2 = await Och(valiMijoz, 100_000, 0, 0);
        var ikkinchi = await Oqi<SmenaDto>(await Yop(valiMijoz, s2.Id, Oxirgi(await Aparatlar(admin), 0m, 50m), terminal: 0, depozit: 0, naqd: 715_000));
        return (ali, vali, birinchi, ikkinchi);
    }

    [Fact]
    public async Task Smena_Jami_AparatBakJadvali_Avans()
    {
        var (_, _, s1, s2) = await Tayyorla();
        Assert.Equal((-1_000L, 5_000L), (s1.Farq, s2.Farq));
        var admin = await Admin();

        var h = await Oqi<HisobotDto>(await admin.GetAsync($"/hisobot?dan={Bugun()}&gacha={Bugun()}"));            // guruh berilmasa = smena
        Assert.Equal([s2.Id.ToString(), s1.Id.ToString()], h.Qatorlar.Select(q => q.Guruh));                       // yangisi tepada
        var q2 = h.Qatorlar[0];
        Assert.Equal(("vali", 1, 50m, 610_000L, 0L, 0L, 610_000L, 5_000L, 0L), (q2.OperatorIsmi, q2.SmenaSoni, q2.Litr, q2.Savdo, q2.Plastik, q2.Depozit, q2.NaqdSavdo, q2.Ortiqcha, q2.Kamomat));
        Assert.Equal((DateOnly.Parse(Bugun()), 0), (q2.Sana, q2.XarajatSoni));
        Assert.Equal((1, 50_000L), (h.Qatorlar[1].XarajatSoni, h.Qatorlar[1].Xarajat));

        var j = h.Jami;
        Assert.Equal(("Jami", true, 2, 150m, 1_830_000L), (j.Guruh, j.Jami, j.SmenaSoni, j.Litr, j.Savdo));
        Assert.Equal((400_000L, 100_000L, 0L, 50_000L, 1), (j.Plastik, j.Depozit, j.Nasiya, j.Xarajat, j.XarajatSoni));
        Assert.Equal((1_830_000L - 400_000 - 100_000, 1_000L, 5_000L), (j.NaqdSavdo, j.Kamomat, j.Ortiqcha));
        Assert.Equal(200_000, h.Avans);

        // Aparat/bak jadvali: 1-aparat: kirim 500 - sotilgan 100 = 400 qoldi (boshida 0); 2-aparat: -50 (kirim yo'q, boshida 0).
        Assert.Equal(5, h.Aparatlar.Length);
        var a1 = h.Aparatlar[0];
        Assert.Equal((1, 0m, 500m, 100m, 400m, 1_220_000L), (a1.Raqam, a1.BakBoshida, a1.Kirim, a1.Sotildi, a1.BakOxirida, a1.Savdo));
        Assert.Equal((0m, 0m, 50m, -50m, 610_000L), (h.Aparatlar[1].BakBoshida, h.Aparatlar[1].Kirim, h.Aparatlar[1].Sotildi, h.Aparatlar[1].BakOxirida, h.Aparatlar[1].Savdo));
        Assert.All(h.Aparatlar, a => Assert.Equal(a.BakBoshida + a.Kirim - a.Sotildi, a.BakOxirida));

        // Operator filtri qatorlar va Jami'ga ta'sir qiladi; avans va bak jadvaliga - yo'q (bak butun shoxobchaniki).
        var faqatAli = await Oqi<HisobotDto>(await admin.GetAsync($"/hisobot?dan={Bugun()}&operatorId={s1.OperatorId}"));
        Assert.Equal([s1.Id.ToString()], faqatAli.Qatorlar.Select(q => q.Guruh));
        Assert.Equal((1, 1_220_000L, 200_000L), (faqatAli.Jami.SmenaSoni, faqatAli.Jami.Savdo, faqatAli.Avans));
        Assert.Equal(150m, faqatAli.Aparatlar.Sum(a => a.Sotildi));
        var faqatVali = await Oqi<HisobotDto>(await admin.GetAsync($"/hisobot?dan={Bugun()}&operatorId={s2.OperatorId}"));
        Assert.Equal(0, faqatVali.Avans);                                                                           // ali'ga yozilgan avans vali'ga tegishli emas
    }

    [Fact]
    public async Task Guruhlar_Kun_Oy_Operator_VaSanaChegaralari()
    {
        var (_, _, s1, s2) = await Tayyorla();
        var admin = await Admin();

        var kun = await Oqi<HisobotDto>(await admin.GetAsync("/hisobot?guruh=kun"));
        var k = Assert.Single(kun.Qatorlar);
        Assert.Equal((Bugun(), DateOnly.Parse(Bugun()), 2, 1_830_000L, null), (k.Guruh, k.Sana, k.SmenaSoni, k.Savdo, k.OperatorIsmi));
        Assert.Equal(1, k.XarajatSoni);

        var oy = Assert.Single((await Oqi<HisobotDto>(await admin.GetAsync("/hisobot?guruh=OY"))).Qatorlar);
        Assert.Equal((Bugun()[..7], null, 2), (oy.Guruh, oy.Sana, oy.SmenaSoni));

        var operatorlar = (await Oqi<HisobotDto>(await admin.GetAsync("/hisobot?guruh=operator"))).Qatorlar;
        Assert.Equal(["ali", "vali"], operatorlar.Select(q => q.Guruh));                                           // ism bo'yicha
        Assert.Equal([1_220_000L, 610_000L], operatorlar.Select(q => q.Savdo));

        // Sana chegaralari (Toshkent): ertadan boshlab ham, kechagacha ham bo'sh.
        Assert.Empty((await Oqi<HisobotDto>(await admin.GetAsync($"/hisobot?dan={Bugun(1)}"))).Qatorlar);
        Assert.Empty((await Oqi<HisobotDto>(await admin.GetAsync($"/hisobot?gacha={Bugun(-1)}"))).Qatorlar);
        var bosh = await Oqi<HisobotDto>(await admin.GetAsync($"/hisobot?dan={Bugun(1)}"));
        Assert.Equal((0, 0L), (bosh.Jami.SmenaSoni, bosh.Jami.Savdo));
        Assert.Equal(s1.Id + 1, s2.Id);

        var (_, op) = await Yarat("sardor");
        await Kut(HttpStatusCode.Forbidden, await op.GetAsync("/hisobot"));
        await Kut(HttpStatusCode.Forbidden, await op.GetAsync("/boshqaruv"));
    }

    [Fact]
    public async Task Boshqaruv_OyKorsatkichlari_JoriyOxirgiVaGrafikSmenalar()
    {
        var (_, _, s1, s2) = await Tayyorla();
        var (_, sardor) = await Yarat("sardor");
        var joriy = await Och(sardor, 100_000, 10, 20);
        var admin = await Admin();

        var b = await Oqi<BoshqaruvDto>(await admin.GetAsync("/boshqaruv"));
        Assert.Equal((joriy.Id, s2.Id), (b.JoriySmena!.Id, b.OxirgiYopilgan!.Id));
        Assert.Equal((100_000L, 10L, 20L, 0L), (b.JoriySmena.OchishQaytim, b.JoriySmena.OchishTerminal, b.JoriySmena.OchishDepozit, b.JoriySmena.Savdo));
        Assert.Equal([s2.Id, s1.Id], b.OxirgiYopilganlar.Select(x => x.Id));                                          // yangisi tepada
        Assert.Equal([s1.Id, s2.Id], b.OxirgiSmenalar.Select(x => x.Id));                                             // eskisidan yangisiga
        Assert.Equal(("ali", 1_220_000L, 100m, -1_000L), (b.OxirgiSmenalar[0].OperatorIsmi, b.OxirgiSmenalar[0].Savdo, b.OxirgiSmenalar[0].Litr, b.OxirgiSmenalar[0].Farq));
        Assert.Equal(DateOnly.Parse(Bugun()), b.OxirgiSmenalar[0].Sana);

        // Oy: yopilgan smenalar (ochiq smena kirmaydi).
        Assert.Equal((1_830_000L, 150m, 2, 1_000L, 5_000L), (b.OySavdo, b.OyLitr, b.OySmenaSoni, b.OyKamomat, b.OyOrtiqcha));
        Assert.Equal((1_330_000L, 400_000L, 100_000L, 0L), (b.OyTolovlar.Naqd, b.OyTolovlar.Plastik, b.OyTolovlar.Depozit, b.OyTolovlar.Nasiya));
        Assert.Equal((0L, 0, 0L, 0), (b.Nasiyalar.FaolQarz, b.Nasiyalar.FaolSoni, b.Nasiyalar.MuddatiOtgan, b.Nasiyalar.MuddatiOtganSoni));

        Assert.Equal(5, b.Aparatlar.Length);
        Assert.Equal((400m, 500m), (b.Aparatlar[0].BakQoldiq, b.Aparatlar[0].OxirgiKirimLitr));
        Assert.NotNull(b.Aparatlar[0].OxirgiKirimVaqti);
    }

    [Fact]
    public async Task Shartnoma_OxirgiSmenaEndpointi_VaMuallifId()
    {
        var (ali, op) = await Yarat("ali");
        var (bosh, boshMijoz) = await Yarat("boshliq", Rol.Boshliq);
        var (_, ruxsatsiz) = await Yarat("mehmon", ruxsatlar: []);                           // hech qanday ruxsati yo'q
        Assert.Equal(HttpStatusCode.NoContent, (await op.GetAsync("/smenalar/oxirgi")).StatusCode);        // yopilgan smena yo'q

        var smena = await Och(op);
        Assert.Equal(HttpStatusCode.NoContent, (await op.GetAsync("/smenalar/oxirgi")).StatusCode);        // ochiq smena "oxirgi yopilgan" emas
        var nasiya = await Oqi<NasiyaDto>(await op.PostAsJsonAsync("/nasiyalar",
            new NasiyaYaratishDto("Bobur", "+998901112233", "", 100_000, DateOnly.Parse(Bugun(5)), null), Json));
        var qaytish = await boshMijoz.PostAsJsonAsync($"/nasiyalar/{nasiya.Id}/qaytish", new NasiyaQaytishiYaratishDto(10_000, TolovTuri.Naqd, false, null), Json);
        await Oqi<NasiyaDto>(qaytish);
        var xarajat = await Oqi<XarajatDto>(await boshMijoz.PostAsJsonAsync("/xarajatlar", new XarajatYaratishDto(5_000, "Sabab", XarajatManbai.Kassa), Json));
        Assert.Equal((ali.Id, bosh.Id), (nasiya.MuallifId, xarajat.MuallifId));                              // ism emas, foydalanuvchi id'si bo'yicha
        var tafsilot = await Oqi<NasiyaTafsilotDto>(await op.GetAsync($"/nasiyalar/{nasiya.Id}"));
        Assert.Equal((bosh.Id, ali.Id), (tafsilot.Qaytishlar.Single().MuallifId, tafsilot.Nasiya.MuallifId));

        await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(await Aparatlar(op), 10m), naqd: 0));
        var oxirgi = await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/oxirgi"));                  // o'z smenasi: to'liq natija
        Assert.Equal(smena.Id, oxirgi.Smena.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await ruxsatsiz.GetAsync("/smenalar/oxirgi")).StatusCode);   // boshqa operator natijani ko'rmaydi (docs 8.10)
        Assert.Equal(smena.Id, (await Oqi<SmenaTopshirishDto>(await ruxsatsiz.GetAsync("/smenalar/oxirgi/topshirish"))).Id);   // topshirish: kirgan har kim
        Assert.Equal((5, 1, 0, 1), (oxirgi.Korsatkichlar.Length, oxirgi.Nasiyalar.Length, oxirgi.Qaytishlar.Length, oxirgi.Xarajatlar.Length));
        Assert.Equal(ali.Id, oxirgi.Nasiyalar[0].MuallifId);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Ilova.CreateClient().GetAsync("/smenalar/oxirgi")).StatusCode);

        // Keyingi yopilgan smena "oxirgi" bo'ladi.
        var ikkinchi = await Och(op);
        await Oqi<SmenaDto>(await Yop(op, ikkinchi.Id, Oxirgi(await Aparatlar(op)), naqd: 0));
        Assert.Equal(ikkinchi.Id, (await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/oxirgi"))).Smena.Id);
    }
}
