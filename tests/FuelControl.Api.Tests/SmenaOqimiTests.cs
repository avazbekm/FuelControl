using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Smenani ochish, davomida yozuvlar, yopish: server formulasi, aparat/bak holati, kamomat harakati, audit.</summary>
public sealed class SmenaOqimiTests : ApiBaza
{
    [Fact]
    public async Task ToliqOqim_OchishNasiyaXarajatYopish_NatijaAparatBakKamomatVaAudit()
    {
        var (sardor, op) = await Yarat("sardor");
        var admin = await Admin();
        var smena = await Och(op, 100_000, 50_000, 200_000);
        Assert.Equal((null, 100_000L, 50_000L, 200_000L, 0L, 0L), (smena.Tugadi, smena.OchishQaytim, smena.OchishTerminal, smena.OchishDepozit, smena.Savdo, smena.Kutilgan));
        Assert.Equal(sardor.Id, smena.OperatorId);

        // Butun shoxobchada bitta ochiq smena: boshqa kishi ham ocholmaydi.
        var ikkinchi = await admin.PostAsJsonAsync("/smenalar/och", new SmenaOchishDto(0, 0, 0), Json);
        await Kut(HttpStatusCode.Conflict, ikkinchi);
        Assert.Contains("ochiq", await Detail(ikkinchi));

        var muddat = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(7);
        await Oqi<NasiyaDto>(await op.PostAsJsonAsync("/nasiyalar", new NasiyaYaratishDto("Mijoz A", "+998901112233", "01 A 111 AA", 350_000, muddat, null), Json));
        var ikkinchiNasiya = await Oqi<NasiyaDto>(await op.PostAsJsonAsync("/nasiyalar", new NasiyaYaratishDto("Mijoz B", "", "01 B 222 BB", 200_000, muddat, "izoh"), Json));
        await Oqi<NasiyaDto>(await op.PostAsJsonAsync($"/nasiyalar/{ikkinchiNasiya.Id}/qaytish", new NasiyaQaytishiYaratishDto(100_000, TolovTuri.Naqd, true, null), Json));
        await Oqi<XarajatDto>(await op.PostAsJsonAsync("/xarajatlar", new XarajatYaratishDto(80_000, "Tozalash", XarajatManbai.Depozit), Json));

        var jonli = await Oqi<SmenaTafsilotDto>(await op.GetAsync("/smenalar/joriy"));
        Assert.Equal((550_000L, 100_000L, 80_000L, 0L), (jonli.Smena.NasiyaJami, jonli.Smena.QaytganNasiya, jonli.Smena.XarajatJami, jonli.Smena.Savdo));
        Assert.Empty(jonli.Korsatkichlar);                       // ochiq smenada savdo yopilganda hisoblanadi
        Assert.Equal((2, 1, 1), (jonli.Nasiyalar.Length, jonli.Qaytishlar.Length, jonli.Xarajatlar.Length));

        // Yopish: aparatlar 100.00 / 50.50 / 20 / 0 / 10.25 L (12 200, 12 200, 15 500, 15 500, 13 800 so'm).
        var aparatlar = await Aparatlar(op);
        var yopildi = await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(aparatlar, 100m, 50.5m, 20m, 0m, 10.25m), terminal: 700_000, depozit: 120_000, naqd: 1_277_550, izoh: "  tinch  "));

        Assert.Equal((2_287_550L, 180.75m, 650_000L, -80_000L), (yopildi.Savdo, yopildi.JamiLitr, yopildi.Plastik, yopildi.DepozitFarqi));
        Assert.Equal((550_000L, 100_000L, 80_000L), (yopildi.NasiyaJami, yopildi.QaytganNasiya, yopildi.XarajatJami));
        Assert.Equal((1_287_550L, 1_277_550L, -10_000L), (yopildi.Kutilgan, yopildi.SanalganNaqd, yopildi.Farq));
        Assert.Equal((700_000L, 120_000L, "tinch"), (yopildi.YopishTerminal, yopildi.YopishDepozit, yopildi.Izoh));
        Assert.NotNull(yopildi.Tugadi);

        Assert.Equal(HttpStatusCode.NoContent, (await op.GetAsync("/smenalar/joriy")).StatusCode);
        var keyin = await Aparatlar(op);
        Assert.Equal([100m, 50.5m, 20m, 0m, 10.25m], keyin.Select(a => a.TotalLitr));           // TotalLitr = yangi ko'rsatkich
        Assert.Equal([-100m, -50.5m, -20m, 0m, -10.25m], keyin.Select(a => a.BakQoldiq));        // bak sotilgan litrga kamaydi (kirim yozilmagan - manfiy ham mumkin)

        var tafsilot = await Oqi<SmenaTafsilotDto>(await admin.GetAsync($"/smenalar/{smena.Id}"));
        Assert.Equal(5, tafsilot.Korsatkichlar.Length);
        var birinchi = tafsilot.Korsatkichlar[0];
        Assert.Equal((0m, 100m, 12_200L, 100m, 1_220_000L, false), (birinchi.Boshi, birinchi.Oxiri, birinchi.Narx, birinchi.Litr, birinchi.Summa, birinchi.NarxOzgarishida));

        // Kamomat operator hisobiga (oylikdan ayiriladi): "Smena #N".
        var hisob = await Oqi<OperatorHisobDto>(await admin.GetAsync($"/operatorlar/{sardor.Id}/hisob"));
        var kamomat = Assert.Single(hisob.Harakatlar, h => h.Turi == HarakatTuri.Kamomat);
        Assert.Equal((-10_000L, $"Smena #{smena.Id}"), (kamomat.Summa, kamomat.Izoh));

        // Audit (dizayndagi uslub, tur bilan).
        var audit = await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=smena", Json);
        Assert.Equal(2, audit!.Length);
        Assert.Contains(audit, a => a.Amal == "Smena ochildi" && a.Tafsilot == $"#{smena.Id} · qaytim puli 100 000 · terminal 50 000 · depozit 200 000");
        Assert.Contains(audit, a => a.Amal == "Smena yopildi" && a.Tafsilot == $"#{smena.Id} · savdo 2 287 550 · 180.75 L · kamomat 10 000" && a.Kim == "sardor");

        // Yopilgan smenani qayta yopib bo'lmaydi; endi yangi smena ochish mumkin.
        await Kut(HttpStatusCode.Conflict, await Yop(op, smena.Id, Oxirgi(keyin), naqd: 0));
        var yangi = await Och(admin, 0, 0, 0);
        Assert.True(yangi.Id > smena.Id);
    }

    [Fact]
    public async Task Yopish_TekshiruvlarRadEtadi_SmenaOchiqQoladi()
    {
        var (_, op) = await Yarat("sardor");
        var smena = await Och(op);
        var aparatlar = await Aparatlar(op);

        // Barcha aparatlar ko'rsatkichi majburiy.
        var yetmaydi = await Yop(op, smena.Id, Oxirgi(aparatlar, 1m, 1m, 1m, 1m).Take(4).ToArray());
        await Kut(HttpStatusCode.BadRequest, yetmaydi);
        Assert.Contains("5-aparat", await Detail(yetmaydi));
        // Bir aparat ikki marta.
        var takror = Oxirgi(aparatlar, 1m, 1m, 1m, 1m, 1m).Append(new AparatKorsatkichDto(aparatlar[0].Id, 5m)).ToArray();
        await Kut(HttpStatusCode.BadRequest, await Yop(op, smena.Id, takror));
        // Manfiy pul maydonlari.
        await Kut(HttpStatusCode.BadRequest, await Yop(op, smena.Id, Oxirgi(aparatlar, 1m, 1m, 1m, 1m, 1m), terminal: -1));
        await Kut(HttpStatusCode.BadRequest, await Yop(op, smena.Id, Oxirgi(aparatlar, 1m, 1m, 1m, 1m, 1m), depozit: -1));
        await Kut(HttpStatusCode.BadRequest, await Yop(op, smena.Id, Oxirgi(aparatlar, 1m, 1m, 1m, 1m, 1m), naqd: -1));
        // Mavjud bo'lmagan smena.
        await Kut(HttpStatusCode.NotFound, await Yop(op, 9999, Oxirgi(aparatlar, 1m, 1m, 1m, 1m, 1m)));

        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("/smenalar/joriy")).StatusCode);
        Assert.Equal([0m, 0m, 0m, 0m, 0m], (await Aparatlar(op)).Select(a => a.TotalLitr));
    }

    [Fact]
    public async Task KichikKorsatkich_RadEtiladi_OldingisidanKichikBolmaydi()
    {
        var (_, op) = await Yarat("sardor");
        var birinchi = await Och(op);
        var aparatlar = await Aparatlar(op);
        await Oqi<SmenaDto>(await Yop(op, birinchi.Id, Oxirgi(aparatlar, 100m, 100m, 100m, 100m, 100m)));

        var ikkinchi = await Och(op);
        var hozirgi = await Aparatlar(op);
        var kichik = Oxirgi(hozirgi, 5m, 5m, 5m, 5m, 5m);
        kichik[2] = kichik[2] with { Qiymat = 99.99m };               // 3-aparat oldingi 100.00 dan kichik
        var javob = await Yop(op, ikkinchi.Id, kichik);
        await Kut(HttpStatusCode.BadRequest, javob);
        Assert.Contains("3-aparat", await Detail(javob));
        Assert.Contains("100.00", await Detail(javob));

        // Teng ko'rsatkich mumkin (aparat ishlamagan): litr 0.
        var teng = await Oqi<SmenaDto>(await Yop(op, ikkinchi.Id, Oxirgi(hozirgi, 0m, 0m, 0m, 0m, 0m)));
        Assert.Equal(0, teng.Savdo);
    }

    [Fact]
    public async Task Ruxsatlar_OchishYopishKorishVaBoshqaOperatorSmenasi()
    {
        var (_, oddiy) = await Yarat("sardor", ruxsatlar: [Ruxsat.Savdo]);               // SmenaOchish yo'q
        await Kut(HttpStatusCode.Forbidden, await oddiy.PostAsJsonAsync("/smenalar/och", new SmenaOchishDto(0, 0, 0), Json));

        var (_, ali) = await Yarat("ali");
        var (_, vali) = await Yarat("vali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var smena = await Och(ali);
        var aparatlar = await Aparatlar(ali);

        // Boshqa operator ko'ra ham, yopa ham olmaydi; boshliq ("Smenalar") ikkalasini ham qiladi.
        await Kut(HttpStatusCode.Forbidden, await vali.GetAsync($"/smenalar/{smena.Id}"));
        await Kut(HttpStatusCode.Forbidden, await Yop(vali, smena.Id, Oxirgi(aparatlar, 1m, 1m, 1m, 1m, 1m)));
        await Oqi<SmenaTafsilotDto>(await ali.GetAsync($"/smenalar/{smena.Id}"));
        await Oqi<SmenaTafsilotDto>(await bosh.GetAsync($"/smenalar/{smena.Id}"));
        // /smenalar ro'yxati: operator faqat o'zinikini ko'radi.
        Assert.Empty((await vali.GetFromJsonAsync<SmenaDto[]>("/smenalar", Json))!);
        Assert.Single((await bosh.GetFromJsonAsync<SmenaDto[]>("/smenalar", Json))!);
        // Joriy smenani istalgan kirgan foydalanuvchi ko'radi (butun shoxobcha bo'yicha bitta).
        Assert.Equal(smena.Id, (await Oqi<SmenaTafsilotDto>(await vali.GetAsync("/smenalar/joriy"))).Smena.Id);

        var yopildi = await Oqi<SmenaDto>(await Yop(bosh, smena.Id, Oxirgi(aparatlar, 1m, 1m, 1m, 1m, 1m)));
        Assert.Equal(smena.OperatorId, yopildi.OperatorId);                                  // kamomat/ortiqcha smena egasiga yoziladi
        Assert.Equal(HttpStatusCode.NotFound, (await vali.GetAsync("/smenalar/9999")).StatusCode);
    }

    [Fact]
    public async Task ParallelIkkiYopish_BittasiOtadi_BakIkkiMartaKamaymaydi()
    {
        var (_, op) = await Yarat("sardor");
        var smena = await Och(op);
        var aparatlar = await Aparatlar(op);
        var korsatkichlar = Oxirgi(aparatlar, 10m, 10m, 10m, 10m, 10m);

        var javoblar = await Task.WhenAll(Yop(op, smena.Id, korsatkichlar), Yop(op, smena.Id, korsatkichlar));

        Assert.Equal(1, javoblar.Count(j => j.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, javoblar.Count(j => j.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal([-10m, -10m, -10m, -10m, -10m], (await Aparatlar(op)).Select(a => a.BakQoldiq));
    }
}
