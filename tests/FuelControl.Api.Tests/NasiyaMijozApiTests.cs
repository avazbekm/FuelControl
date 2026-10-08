using System.Net;
using System.Net.Http.Json;
using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Telefon formati, GET /nasiyalar/mijozlar va /nasiyalar?q= uchun yagona qidiruv qoidasi (docs 8-bo'lim, 1-3 bandlar).</summary>
public sealed class NasiyaMijozApiTests : ApiBaza
{
    private const string Standart = "+998 90 123 45 67";
    private static DateOnly Bugun => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));

    private static NasiyaYaratishDto Yangi(string ism, string tel, string raqam, long summa = 100_000) => new(ism, tel, raqam, summa, Bugun.AddDays(10), null);

    private static Task<HttpResponseMessage> Yoz(HttpClient m, NasiyaYaratishDto d) => m.PostAsJsonAsync("/nasiyalar", d, Json);

    private static async Task<NasiyaDto> Yoz(HttpClient m, string ism, string tel, string raqam = "", long summa = 100_000) =>
        await Oqi<NasiyaDto>(await Yoz(m, Yangi(ism, tel, raqam, summa)));

    private static Task<MijozTaklifDto[]> Takliflar(HttpClient m, string? q) =>
        m.GetFromJsonAsync<MijozTaklifDto[]>("/nasiyalar/mijozlar" + (q is null ? "" : "?q=" + Uri.EscapeDataString(q)), Json)!;

    private static async Task<NasiyaDto[]> Royxat(HttpClient m, string q) =>
        (await m.GetFromJsonAsync<NasiyalarDto>("/nasiyalar?q=" + Uri.EscapeDataString(q), Json))!.Royxat;

    [Fact]
    public async Task Yozish_TelefonHarQandayKorinishda_StandartFormatdaSaqlanadiVaQaytadi()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);

        foreach (var kiritilgan in new[] { "90 123 45 67", "998901234567", "+998-90-123-45-67", "+998 (90) 123-45-67", " 901234567 ", Standart })
        {
            var n = await Yoz(op, "Jasur", kiritilgan);
            Assert.Equal(Standart, n.Telefon);
            Assert.Equal(Standart, (await Oqi<NasiyaTafsilotDto>(await op.GetAsync($"/nasiyalar/{n.Id}"))).Nasiya.Telefon);
        }

        var royxat = (await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar"))).Royxat;
        Assert.Equal(6, royxat.Length);
        Assert.All(royxat, n => Assert.Equal(Standart, n.Telefon));
        using var scope = Ilova.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FuelControlDbContext>();
        Assert.All(await db.Nasiyalar.Select(n => n.Telefon).ToListAsync(), t => Assert.Equal(Standart, t));
    }

    [Fact]
    public async Task Yozish_AynanToqqizRaqamEmas_400_NasiyaYozilmaydi()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);

        foreach (var notogri in new[] { "90 123 45 6", "+998 90 123 45 678", "901234567 8", "abc", "12345", "+998 90" })
        {
            var javob = await Yoz(op, Yangi("Jasur", notogri, "01 A 777 BC"));     // mashina raqami bor, lekin berilgan telefon noto'g'ri
            await Kut(HttpStatusCode.BadRequest, javob);
            Assert.Contains("Telefon", await Detail(javob));
        }

        Assert.Empty((await Oqi<NasiyalarDto>(await op.GetAsync("/nasiyalar"))).Royxat);
    }

    [Fact]
    public async Task Yozish_BoshTelefon_FaqatMashinaRaqamiBilanMumkin()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);

        Assert.Equal("", (await Yoz(op, "Bir", "", "01 A 111 AA")).Telefon);
        Assert.Equal("", (await Yoz(op, "Ikki", "+998 ", "01 A 222 AA")).Telefon);       // maydondagi o'chmaydigan prefiks - telefon yo'q
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, Yangi("Uch", "+998 ", "")));  // telefon ham, raqam ham yo'q
        await Kut(HttpStatusCode.BadRequest, await Yoz(op, Yangi("To'rt", "", " ")));
    }

    [Fact]
    public async Task Mijozlar_Ruxsatlar_NasiyaYozishQarzQaytdiYokiNasiyalar()
    {
        var (_, yozuvchi) = await Yarat("yozuvchi", ruxsatlar: [Ruxsat.NasiyaYozish]);
        var (_, qaytaruvchi) = await Yarat("qaytaruvchi", ruxsatlar: [Ruxsat.QarzQaytdi]);
        var (_, korguvchi) = await Yarat("korguvchi", ruxsatlar: [Ruxsat.Nasiyalar]);
        var (_, boshqa) = await Yarat("boshqa", ruxsatlar: [Ruxsat.Savdo, Ruxsat.SmenaOchish, Ruxsat.XarajatYozish]);

        foreach (var mijoz in new[] { yozuvchi, qaytaruvchi, korguvchi })
            Assert.Empty(await Takliflar(mijoz, null));
        await Kut(HttpStatusCode.Forbidden, await boshqa.GetAsync("/nasiyalar/mijozlar"));
        await Kut(HttpStatusCode.Unauthorized, await Ilova.CreateClient().GetAsync("/nasiyalar/mijozlar"));
    }

    [Fact]
    public async Task Mijozlar_TelefonBoyichaGuruhlanadi_FaolQarzVaOxirgiNasiya()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        await Yoz(op, "Jasur To'xtayev", "+998 90 123 45 67", "01 A 777 BC", 350_000);
        var ikkinchi = await Yoz(op, "Jasur Toxtayev", "90 123 45 67", "01 B 111 AA", 200_000);      // shu telefon - bir mijoz
        await Oqi<NasiyaDto>(await op.PostAsJsonAsync($"/nasiyalar/{ikkinchi.Id}/qaytish", new NasiyaQaytishiYaratishDto(50_000, TolovTuri.Naqd, true, null), Json));
        await Yoz(op, "Farhod Ismoilov", "+998 93 555 12 34", "30 B 456 CA", 220_000);
        await Yoz(op, "Ali Valiyev", "", "01 K 100 KA");                                              // telefonsiz: ism + mashina raqami bo'yicha
        await Yoz(op, "ali valiyev", "", "01k100ka", 40_000);                                         // o'sha mijoz

        var t = await Takliflar(op, null);

        // Yangisi oldinda; taklifdagi ism/raqam - mijozning eng yangi yozuvidan (qanday yozilgan bo'lsa shunday).
        Assert.Equal(["ali valiyev", "Farhod Ismoilov", "Jasur Toxtayev"], t.Select(x => x.MijozIsmi));
        var jasur = t[2];
        Assert.Equal((Standart, "01 B 111 AA", 2, 500_000L, Bugun), (jasur.Telefon, jasur.MashinaRaqami, jasur.NasiyaSoni, jasur.FaolQarz, jasur.OxirgiNasiya));
        var ali = t[0];
        Assert.Equal(("", 2, 140_000L), (ali.Telefon, ali.NasiyaSoni, ali.FaolQarz));
    }

    [Fact]
    public async Task Qidiruv_IkkalaEndpointdaBirXil_IsmApostrofTelefonBolagiMashinaRaqami()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        var birinchi = await Yoz(op, "Jasur To'xtayev", "+998 90 123 45 67", "01 A 777 BC", 350_000);
        var ikkinchi = await Yoz(op, "Jasur Toxtayev", "90 123 45 67", "01 B 111 AA", 200_000);
        var farhod = await Yoz(op, "Farhod Ismoilov", "+998 93 555 12 34", "30 B 456 CA", 220_000);

        // (so'rov, nasiyalar ro'yxatidagi kutilgan id'lar (yangisi oldinda), takliflardagi kutilgan ismlar: mijoz bitta, eng mos/yangi yozuvdan)
        var holatlar = new (string Q, int[] Idlar, string[] Ismlar)[]
        {
            ("To" + (char)0x2018 + "xtayev", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),   // apostrof tashlanadi: ikkala yozuv, mijoz bitta
            ("to" + (char)0x2019 + "XTAYEV", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),
            ("TO`XTAYEV", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),
            ("TOXTAYEV", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),
            ("4567", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),                      // telefon bo'lagi
            ("+998 90 123", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),
            ("45-67", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),
            ("777bc", [birinchi.Id], ["Jasur To'xtayev"]),                                 // mashina raqami bo'lagi
            ("111 aa", [ikkinchi.Id], ["Jasur Toxtayev"]),
            ("555", [farhod.Id], ["Farhod Ismoilov"]),
            ("JASUR", [ikkinchi.Id, birinchi.Id], ["Jasur Toxtayev"]),
            ("topilmaydi", [], []),
        };
        foreach (var (q, idlar, ismlar) in holatlar)
        {
            Assert.True(idlar.SequenceEqual((await Royxat(op, q)).Select(n => n.Id)), $"/nasiyalar?q={q}");
            Assert.True(ismlar.SequenceEqual((await Takliflar(op, q)).Select(t => t.MijozIsmi)), $"/nasiyalar/mijozlar?q={q}");
        }
    }

    [Fact]
    public async Task Qidiruv_ApostrofTashlanadi_UlugbekVaToxtayev_IkkalaEndpointda()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        var nazarov = await Yoz(op, "Ulug'bek Nazarov", "+998 91 333 44 55", "01 K 515 KA");
        var karimov = await Yoz(op, "Ulugbek Karimov", "+998 90 000 00 02", "01 U 002 UU");
        var toxtayev = await Yoz(op, "Jasur Toxtayev", "+998 90 000 00 03", "01 A 003 AA");

        foreach (var q in new[] { "ulugbek", "Ulug'bek", "ULUG" + (char)0x2018 + "BEK", "ulug" + (char)0x02BB + "bek" })
        {
            Assert.Equal([karimov.Id, nazarov.Id], (await Royxat(op, q)).Select(n => n.Id));
            Assert.Equal(["Ulugbek Karimov", "Ulug'bek Nazarov"], (await Takliflar(op, q)).Select(t => t.MijozIsmi));
        }
        foreach (var q in new[] { "To'xtayev", "TO`XTAYEV", "toxtayev" })
        {
            Assert.Equal([toxtayev.Id], (await Royxat(op, q)).Select(n => n.Id));
            Assert.Equal(["Jasur Toxtayev"], (await Takliflar(op, q)).Select(t => t.MijozIsmi));
        }
        Assert.Equal([nazarov.Id], (await Royxat(op, "Ulugbek Nazarov")).Select(n => n.Id));        // aynan mos

        // Telefonsiz mijoz: ism + mashina raqami bo'yicha guruhlanadi, apostrofli va apostrofsiz yozuv - bir mijoz.
        await Yoz(op, "Sa'id Karimov", "", "01 S 777 SS");
        await Yoz(op, "Said Karimov", "", "01s777ss");
        var said = (await Takliflar(op, "said")).Single();
        Assert.Equal((2, 200_000L), (said.NasiyaSoni, said.FaolQarz));
    }

    [Fact]
    public async Task Qidiruv_TelefondaKamidaUchtaRaqam_YolgizPrefiksHechNarsagaMosEmas()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        await Yoz(op, "Jasur", Standart);

        foreach (var q in new[] { "12", "+998", "+998 9", "+998 90" })
        {
            Assert.Empty(await Royxat(op, q));
            Assert.Empty(await Takliflar(op, q));
        }
        Assert.Single(await Royxat(op, "123"));
        Assert.Single(await Takliflar(op, "123"));
    }

    [Fact]
    public async Task Tartib_AynanVaBoshidanMosOldin_KeyinQolganlari_YangisiOldinda()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        var vali = await Yoz(op, "Vali Karimov", "+998 90 000 00 01");        // ichidan
        var alisher = await Yoz(op, "Alisher", "+998 90 000 00 02");           // boshidan
        var ali = await Yoz(op, "Ali", "+998 90 000 00 03");                   // aynan
        var alijon = await Yoz(op, "Alijon", "+998 90 000 00 04");             // boshidan, yangiroq

        Assert.Equal([ali.Id, alijon.Id, alisher.Id, vali.Id], (await Royxat(op, "ali")).Select(n => n.Id));
        Assert.Equal(["Ali", "Alijon", "Alisher", "Vali Karimov"], (await Takliflar(op, "ali")).Select(t => t.MijozIsmi));
    }

    [Fact]
    public async Task Mijozlar_EngKopi8Ta()
    {
        var (_, op) = await Yarat("ali");
        await Och(op);
        for (var i = 1; i <= 10; i++) await Yoz(op, "Mijoz " + i, "+998 90 000 00 " + i.ToString("00"));

        var hammasi = await Takliflar(op, null);

        Assert.Equal(8, hammasi.Length);
        Assert.Equal(["Mijoz 10", "Mijoz 9", "Mijoz 8", "Mijoz 7", "Mijoz 6", "Mijoz 5", "Mijoz 4", "Mijoz 3"], hammasi.Select(t => t.MijozIsmi));
        Assert.Equal(8, (await Takliflar(op, "mijoz")).Length);
        Assert.Equal(2, (await Takliflar(op, "mijoz 1")).Length);                  // "Mijoz 1" va "Mijoz 10"
    }
}
