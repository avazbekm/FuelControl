using System.Net;
using System.Net.Http.Json;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Xunit;

namespace FuelControl.Api.Tests;

/// <summary>Har aparatning o'z baki: kirim (litrda), qo'lda tuzatish (sabab bilan), kirimlar ro'yxati va ruxsatlar.</summary>
public sealed class BakKirimApiTests : ApiBaza
{
    private static Task<HttpResponseMessage> Kirim(HttpClient m, int aparatId, decimal litr, DateTime? vaqt = null, string? hujjat = null) =>
        m.PostAsJsonAsync($"/aparatlar/{aparatId}/kirim", new BakKirimYaratishDto(litr, vaqt, hujjat), Json);

    [Fact]
    public async Task Kirim_BakOshadi_PultOzgarmaydi_OxirgiKirimVaAudit()
    {
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var a = (await Aparatlar(bosh))[4];                                      // 5-aparat, Dizel
        Assert.Equal((0m, null), (a.BakQoldiq, a.OxirgiKirimVaqti));

        var natija = await Oqi<AparatDto>(await Kirim(bosh, a.Id, 8_000m, null, "  yuk xati 1176 "));
        Assert.Equal((8_000m, 0m, 8_000m), (natija.BakQoldiq, natija.TotalLitr, natija.OxirgiKirimLitr));       // "Pult ko'rsatkichi o'zgarmaydi"
        Assert.NotNull(natija.OxirgiKirimVaqti);

        // Orqaga sanalangan kirim: bak oshadi, lekin "oxirgi kirim" vaqt bo'yicha eng oxirgisi bo'lib qoladi.
        var kecha = DateTime.UtcNow.AddDays(-1);
        var ikkinchi = await Oqi<AparatDto>(await Kirim(bosh, a.Id, 1_250.5m, kecha));
        Assert.Equal((9_250.5m, 8_000m), (ikkinchi.BakQoldiq, ikkinchi.OxirgiKirimLitr));

        var royxat = (await bosh.GetFromJsonAsync<BakKirimDto[]>($"/bak-kirimlar?aparatId={a.Id}", Json))!;
        Assert.Equal(2, royxat.Length);
        Assert.Equal((a.Id, 5, 8_000m, 0m, 8_000m, "yuk xati 1176", "boshliq"), (royxat[0].AparatId, royxat[0].AparatRaqam, royxat[0].Litr, royxat[0].QoldiqOldin, royxat[0].QoldiqKeyin, royxat[0].Hujjat, royxat[0].KimYozdi));
        Assert.Equal((1_250.5m, 8_000m, 9_250.5m, null), (royxat[1].Litr, royxat[1].QoldiqOldin, royxat[1].QoldiqKeyin, royxat[1].Hujjat));
        Assert.Empty((await bosh.GetFromJsonAsync<BakKirimDto[]>($"/bak-kirimlar?aparatId={(await Aparatlar(bosh))[0].Id}", Json))!);
        var bugun = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        Assert.Single((await bosh.GetFromJsonAsync<BakKirimDto[]>($"/bak-kirimlar?dan={bugun:yyyy-MM-dd}", Json))!);     // bugungisi (kecha sanalgani emas)

        var audit = (await (await Admin()).GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=bak", Json))!;
        Assert.Contains(audit, x => x.Amal == "Bakka kirim" && x.Tafsilot == "5-aparat, Dizel · +8 000 L · bak 8 000 L bo'ldi · yuk xati 1176");
        Assert.Contains(audit, x => x.Tafsilot == "5-aparat, Dizel · +1 250.50 L · bak 9 250.50 L bo'ldi");
    }

    [Fact]
    public async Task Kirim_Validatsiya_Ruxsat_NomalumAparat()
    {
        var (_, bosh) = await Yarat("boshliq", Rol.Boshliq);
        var (_, op) = await Yarat("ali");
        var a = (await Aparatlar(bosh))[0];
        await Kut(HttpStatusCode.BadRequest, await Kirim(bosh, a.Id, 0m));
        await Kut(HttpStatusCode.BadRequest, await Kirim(bosh, a.Id, -10m));
        await Kut(HttpStatusCode.BadRequest, await Kirim(bosh, a.Id, 10m, DateTime.UtcNow.AddDays(3)));       // kelajak
        await Kut(HttpStatusCode.NotFound, await Kirim(bosh, 9999, 10m));
        await Kut(HttpStatusCode.Forbidden, await Kirim(op, a.Id, 10m));                                      // operatorda BakKirim ruxsati yo'q
        Assert.Equal(0m, (await Aparatlar(bosh))[0].BakQoldiq);

        // /bak-kirimlar: Hisobotlar yoki BakKirim ruxsati kerak.
        await Kut(HttpStatusCode.Forbidden, await op.GetAsync("/bak-kirimlar"));
        var (_, hisobotchi) = await Yarat("hisobchi", ruxsatlar: [Ruxsat.Hisobotlar]);
        await Kut(HttpStatusCode.OK, await hisobotchi.GetAsync("/bak-kirimlar"));
        var (_, bakchi) = await Yarat("bakchi", ruxsatlar: [Ruxsat.BakKirim]);
        await Kut(HttpStatusCode.OK, await bakchi.GetAsync("/bak-kirimlar"));
        await Kut(HttpStatusCode.OK, await Kirim(bakchi, a.Id, 5m));
    }

    [Fact]
    public async Task Yaratish_BoshlangichBak_Tuzatish_SababMajburiy_Tarix()
    {
        var admin = await Admin();
        var yoqilgi = (await admin.GetFromJsonAsync<YoqilgiTuriDto[]>("/yoqilgilar", Json))![0];
        var yangi = await Oqi<AparatDto>(await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(6, yoqilgi.Id, 100m, 1_500m), Json));
        Assert.Equal((100m, 1_500m), (yangi.TotalLitr, yangi.BakQoldiq));
        await Kut(HttpStatusCode.BadRequest, await admin.PostAsJsonAsync("/aparatlar", new AparatYaratishDto(7, yoqilgi.Id, 0m, -1m), Json));

        var yol = $"/aparatlar/{yangi.Id}";
        var sababsiz = await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(6, yoqilgi.Id, null, 1_400m), Json);
        await Kut(HttpStatusCode.BadRequest, sababsiz);
        Assert.Equal(1_500m, (await Aparatlar(admin)).Single(x => x.Id == yangi.Id).BakQoldiq);
        await Kut(HttpStatusCode.BadRequest, await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(6, yoqilgi.Id, null, -5m, "o'lchov"), Json));

        var tuzatildi = await Oqi<AparatDto>(await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(6, yoqilgi.Id, null, 1_400m, "  Chizg'ich bilan o'lchandi "), Json));
        Assert.Equal(1_400m, tuzatildi.BakQoldiq);
        // Bir xil qiymat: o'zgarish ham, audit ham yo'q (sabab ham kerak emas).
        await Kut(HttpStatusCode.OK, await admin.PutAsJsonAsync(yol, new AparatTahrirlashDto(6, yoqilgi.Id, null, 1_400m), Json));

        var audit = (await admin.GetFromJsonAsync<AuditYozuviDto[]>("/audit?tur=bak", Json))!;
        var yozuv = Assert.Single(audit, x => x.Amal == "Bak qoldig'i tuzatildi");
        Assert.Equal("6-aparat bak qoldig'i 1 500.00 dan 1 400.00 ga · sabab: Chizg'ich bilan o'lchandi", yozuv.Tafsilot);
    }
}
