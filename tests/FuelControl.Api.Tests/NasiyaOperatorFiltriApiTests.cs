using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Spec 8.12: GET /nasiyalar?operatorId= (Nasiya.OperatorId = NasiyaDto.MuallifId) va NasiyalarDto.Operator xulosasi; eski qarz faqat EskiQarzQabul da.</summary>
public sealed class NasiyaOperatorFiltriApiTests : ApiBaza
{
    private async Task<NasiyalarDto> Royxat(HttpClient m, string sorov = "") => await Oqi<NasiyalarDto>(await m.GetAsync("/nasiyalar" + sorov));

    [Fact]
    public async Task OperatorFiltri_RoyxatVaXulosa_HolatVaQBilanBirga_EskiQarzFaqatEskiQarzQabulda()
    {
        var (ali, aliM) = await Yarat("ali");
        var (vali, valiM) = await Yarat("vali");
        var admin = await Admin();
        await Och(aliM);
        var a = await Oqi<NasiyaDto>(await EskiQarzApiTests.OddiyYoz(aliM, 100_000, "Bobur Aliyev"));
        var b = await Oqi<NasiyaDto>(await EskiQarzApiTests.OddiyYoz(aliM, 50_000, "Vali Karimov"));
        await Kut(HttpStatusCode.OK, await aliM.PostAsJsonAsync($"/nasiyalar/{a.Id}/qaytish", new NasiyaQaytishiYaratishDto(40_000, TolovTuri.Naqd, true, null), Json));
        var e1 = await Oqi<NasiyaTafsilotDto>(await EskiQarzApiTests.EskiYoz(aliM, EskiQarzApiTests.Eski(300_000, ism: "Hamid")));
        var c = await Oqi<NasiyaDto>(await EskiQarzApiTests.OddiyYoz(valiM, 70_000, "Bobur Aliyev"));
        var e2 = await Oqi<NasiyaTafsilotDto>(await EskiQarzApiTests.EskiYoz(valiM, EskiQarzApiTests.Eski(10_000, ism: "Sher")));

        var hammasi = await Royxat(admin);
        Assert.Null(hammasi.Operator);                                                                  // operatorId berilmasa - to'ldirilmaydi
        Assert.Equal(5, hammasi.Royxat.Length);

        var aliRoyxat = await Royxat(admin, $"?operatorId={ali.Id}");
        Assert.Equal(new[] { a.Id, b.Id, e1.Nasiya.Id }, aliRoyxat.Royxat.Select(x => x.Id).Order());
        Assert.All(aliRoyxat.Royxat, x => Assert.Equal(ali.Id, x.MuallifId));
        var aliXulosa = new NasiyaOperatorXulosaDto(ali.Id, "ali", Soni: 2, Berilgan: 150_000, Qaytgan: 40_000, Qoldiq: 110_000, FaolSoni: 2, EskiQarzQabul: 300_000);
        Assert.Equal(aliXulosa, aliRoyxat.Operator);

        // Umumiy kartalar (Xulosa) filtrga bog'liq emas; "oy berilgan" faqat oddiy nasiyalar (50 000 + 100 000 + 70 000), "oy qaytgan" - hamma qaytish.
        Assert.Equal(hammasi.Xulosa, aliRoyxat.Xulosa);
        Assert.Equal((220_000L, 3, 350_000L, 3), (hammasi.Xulosa.OyBerilgan, hammasi.Xulosa.OyBerilganSoni, hammasi.Xulosa.OyQaytgan, hammasi.Xulosa.OyQaytganSoni));
        Assert.Equal((180_000L, 3), (hammasi.Xulosa.FaolQarz, hammasi.Xulosa.FaolSoni));

        // holat va q bilan birga ishlaydi; operator xulosasi ularga bog'liq emas.
        var faol = await Royxat(admin, $"?operatorId={ali.Id}&holat=faol");
        Assert.Equal(new[] { a.Id, b.Id }, faol.Royxat.Select(x => x.Id).Order());
        Assert.Equal(aliXulosa, faol.Operator);
        var yopilgan = await Royxat(admin, $"?operatorId={ali.Id}&holat=yopilgan");
        Assert.Equal(new[] { e1.Nasiya.Id }, yopilgan.Royxat.Select(x => x.Id));                        // eski qarz yopilgan, belgisi bilan
        Assert.True(yopilgan.Royxat[0].EskiQarz);
        var qidiruv = await Royxat(admin, $"?operatorId={ali.Id}&q=bobur");
        Assert.Equal(new[] { a.Id }, qidiruv.Royxat.Select(x => x.Id));                                 // vali yozgan "Bobur Aliyev" kirmaydi
        Assert.Equal(aliXulosa, qidiruv.Operator);
        Assert.Equal(new[] { a.Id, c.Id }, (await Royxat(admin, "?q=bobur")).Royxat.Select(x => x.Id).Order());

        var valiRoyxat = await Royxat(admin, $"?operatorId={vali.Id}");
        Assert.Equal(new[] { c.Id, e2.Nasiya.Id }, valiRoyxat.Royxat.Select(x => x.Id).Order());
        Assert.Equal(new NasiyaOperatorXulosaDto(vali.Id, "vali", 1, 70_000, 0, 70_000, 1, 10_000), valiRoyxat.Operator);

        var yoq = await Royxat(admin, "?operatorId=9999");                                              // yozuvi yo'q operator: bo'sh ro'yxat, nol xulosa
        Assert.Empty(yoq.Royxat);
        Assert.Equal(new NasiyaOperatorXulosaDto(9999, "", 0, 0, 0, 0, 0, 0), yoq.Operator);
        await Kut(HttpStatusCode.BadRequest, await admin.GetAsync($"/nasiyalar?operatorId={ali.Id}&holat=xato"));
        await Kut(HttpStatusCode.Forbidden, await (await Yarat("sardor", ruxsatlar: [Ruxsat.Savdo])).Mijoz.GetAsync($"/nasiyalar?operatorId={ali.Id}"));
    }

    [Fact]
    public async Task OperatorXulosasi_OperatorOzgarganIsmi_HozirgiIsmBilan()
    {
        var (ali, aliM) = await Yarat("ali");
        var admin = await Admin();
        await Och(aliM);
        await EskiQarzApiTests.OddiyYoz(aliM, 25_000);
        (await admin.PutAsJsonAsync($"/foydalanuvchilar/{ali.Id}", new FoydalanuvchiTahrirlashDto("Ali Valiyev", Rol.Operator, true, 4_000_000), Json)).EnsureSuccessStatusCode();

        var javob = await Royxat(admin, $"?operatorId={ali.Id}");
        Assert.Equal("Ali Valiyev", javob.Operator!.OperatorIsmi);                                      // foydalanuvchining hozirgi ismi
        Assert.Equal("ali", javob.Royxat[0].OperatorIsmi);                                              // nasiya yozuvida esa yozilgan paytdagi ism
    }
}
