using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Audit: har yozuvda tur (smena | nasiya | xarajat | bak | tuzatish | hisob | sozlama | kirish), tur filtri, qidiruv, sana, ruxsat.</summary>
public sealed class AuditApiTests : ApiBaza
{
    [Fact]
    public async Task TurFiltri_Qidiruv_Sana_Limit_VaRuxsat()
    {
        var admin = await Admin();
        var (ali, op) = await Yarat("ali");                                                                 // "Foydalanuvchi yaratildi" - sozlama
        await admin.PostAsJsonAsync($"/foydalanuvchilar/{ali.Id}/pin", new PinOrnatishDto("2222"), Json);   // "Parol/PIN almashtirildi" - kirish
        await admin.PostAsJsonAsync($"/operatorlar/{ali.Id}/harakat", new HarakatYaratishDto(HarakatTuri.Avans, 100_000, "Naqd berildi"), Json);   // hisob
        await admin.PostAsJsonAsync("/audit/eksport", new AuditEksportDto("Hisob-varaqa", "ali"), Json);   // hisob
        await admin.PostAsJsonAsync("/audit/eksport", new AuditEksportDto("Hisobot", "01.10 - 04.10"), Json);   // sozlama

        async Task<AuditYozuviDto[]> Tur(string t) => (await admin.GetFromJsonAsync<AuditYozuviDto[]>($"/audit?tur={t}", Json))!;
        var sozlama = await Tur("sozlama");
        Assert.Contains(sozlama, a => a.Amal == "Foydalanuvchi yaratildi" && a.Kim == "Administrator");
        Assert.Contains(sozlama, a => a.Amal == "Eksport: Hisobot");
        Assert.Equal(["Parol/PIN almashtirildi"], (await Tur("kirish")).Select(a => a.Amal));
        var hisob = await Tur("hisob");
        Assert.Equal(["Eksport: Hisob-varaqa", "Avans berildi"], hisob.Select(a => a.Amal));                 // yangisi tepada
        Assert.Equal("ali · 100 000 · Naqd berildi", hisob[1].Tafsilot);
        Assert.Empty(await Tur("smena"));
        Assert.All(await Tur("SOZLAMA"), a => Assert.Equal("sozlama", a.Tur));                                // katta-kichik harfga bog'liq emas
        Assert.All((await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit", Json))!, a => Assert.Contains(a.Tur, AuditTurlari.Hammasi));

        var noma = await admin.GetAsync("/audit?tur=noma'lum");
        await Kut(HttpStatusCode.BadRequest, noma);
        Assert.Contains("smena, nasiya, xarajat, bak, tuzatish, hisob, sozlama, kirish", await Detail(noma));

        Assert.Single((await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?limit=1", Json))!);
        Assert.Equal(["Avans berildi"], (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?q=Avans", Json))!.Select(a => a.Amal));
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        Assert.NotEmpty((await admin.GetFromJsonAsync<AuditYozuviDto[]>($"/audit?dan={bugun:yyyy-MM-dd}&gacha={bugun:yyyy-MM-dd}", Json))!);
        Assert.Empty((await admin.GetFromJsonAsync<AuditYozuviDto[]>($"/audit?dan={bugun.AddDays(1):yyyy-MM-dd}", Json))!);
        await Kut(HttpStatusCode.Forbidden, await op.GetAsync("/audit"));                                       // operatorda Audit ruxsati yo'q
    }

    [Fact]
    public async Task Amallar_TuriBilanYoziladi_SmenaNasiyaXarajatBakTuzatish()
    {
        var (_, op) = await Yarat("ali");
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var admin = await Admin();
        var aparatlar = await Aparatlar(admin);
        await Oqi<AparatDto>(await bosh.PostAsJsonAsync($"/aparatlar/{aparatlar[0].Id}/kirim", new BakKirimYaratishDto(100m, null, null), Json));
        var smena = await Och(op);
        var muddat = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)).AddDays(5);
        var nasiya = await Oqi<NasiyaDto>(await op.PostAsJsonAsync("/nasiyalar", new NasiyaYaratishDto("Bobur", "+998901112233", "", 100_000, muddat, null), Json));
        await Oqi<NasiyaDto>(await op.PostAsJsonAsync($"/nasiyalar/{nasiya.Id}/qaytish", new NasiyaQaytishiYaratishDto(10_000, TolovTuri.Plastik, true, null), Json));
        await Oqi<XarajatDto>(await op.PostAsJsonAsync("/xarajatlar", new XarajatYaratishDto(5_000, "Sabab", XarajatManbai.Depozit), Json));
        await Oqi<SmenaDto>(await Yop(op, smena.Id, Oxirgi(await Aparatlar(op), 10m), naqd: 0));
        await Oqi<SmenaTafsilotDto>(await bosh.PutAsJsonAsync($"/smenalar/{smena.Id}/korsatkich", new KorsatkichTuzatishDto(aparatlar[0].Id, 12m, "Xato"), Json));

        var barchasi = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?limit=100", Json))!;
        (string Amal, string Tur)[] kutilgan =
        [
            ("Bakka kirim", "bak"), ("Smena ochildi", "smena"), ("Nasiya yozildi", "nasiya"), ("Qarz qaytdi", "nasiya"),
            ("Xarajat yozildi", "xarajat"), ("Smena yopildi", "smena"), ("Ko'rsatkich tuzatildi", "tuzatish"),
        ];
        foreach (var (amal, tur) in kutilgan)
            Assert.Contains(barchasi, a => a.Amal == amal && a.Tur == tur);
        Assert.Equal("Bobur · +998 90 111 22 33 · 100 000 · " + $"{muddat:dd.MM} gacha", barchasi.Single(a => a.Amal == "Nasiya yozildi").Tafsilot);   // mashina raqami yo'q - telefon
        Assert.Equal("Bobur · 10 000 plastik · qolgan qarz 90 000", barchasi.Single(a => a.Amal == "Qarz qaytdi").Tafsilot);
    }
}
