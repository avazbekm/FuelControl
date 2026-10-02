using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FuelControl.Contracts.Dto;

namespace FuelControl.Desktop.Services;

/// <summary>Server rad etgan amal yoki aloqa xatosi. Xabar foydalanuvchiga ko'rsatiladi (server ProblemDetails'dagi o'zbekcha matn).</summary>
public sealed class ApiXatosi(string xabar, int status) : Exception(xabar)
{
    public int Status { get; } = status;
    public bool AloqaXatosi => Status == 0;
}

/// <summary>FuelControl API uchun tipli mijoz. Token login'dan keyin saqlanadi va har so'rovga qo'shiladi.</summary>
public sealed class ApiMijoz
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly HttpClient _http;

    public ApiMijoz(string manzil)
    {
        Manzil = manzil.TrimEnd('/');
        _http = new HttpClient { BaseAddress = new Uri(Manzil + "/"), Timeout = TimeSpan.FromSeconds(15) };
    }

    public string Manzil { get; }
    public string? Token { get; private set; }

    /// <summary>Token muddati tugagan yoki bekor qilingan (401) — qayta kirish kerak.</summary>
    public event Action? SessiyaTugadi;

    // ---------- Auth ----------
    public async Task<LoginJavobiDto> Kirish(string login, string parolYokiPin)
    {
        var javob = await Yubor<LoginJavobiDto>(HttpMethod.Post, "auth/login", new LoginSoroviDto(login, parolYokiPin), tokenli: false);
        Token = javob!.Token;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return javob;
    }

    public void Chiqish()
    {
        Token = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    public Task<FoydalanuvchiDto?> Men() => Ol<FoydalanuvchiDto>("me");

    // ---------- Yoqilg'i / aparat ----------
    public Task<List<YoqilgiTuriDto>?> Yoqilgilar() => Ol<List<YoqilgiTuriDto>>("yoqilgilar");
    public Task<List<NarxTarixiDto>?> NarxTarixi() => Ol<List<NarxTarixiDto>>("yoqilgilar/narx-tarixi");
    public Task<YoqilgiTuriDto?> YoqilgiYarat(YoqilgiYaratishDto d) => Yubor<YoqilgiTuriDto>(HttpMethod.Post, "yoqilgilar", d);
    public Task<YoqilgiTuriDto?> YoqilgiTahrirla(int id, YoqilgiTahrirlashDto d) => Yubor<YoqilgiTuriDto>(HttpMethod.Put, $"yoqilgilar/{id}", d);
    public Task YoqilgiOchir(int id) => Yubor<object>(HttpMethod.Delete, $"yoqilgilar/{id}", null);

    public Task<List<AparatDto>?> Aparatlar() => Ol<List<AparatDto>>("aparatlar");
    public Task<AparatDto?> AparatYarat(AparatYaratishDto d) => Yubor<AparatDto>(HttpMethod.Post, "aparatlar", d);
    public Task<AparatDto?> AparatTahrirla(int id, AparatTahrirlashDto d) => Yubor<AparatDto>(HttpMethod.Put, $"aparatlar/{id}", d);

    // ---------- Foydalanuvchilar ----------
    public Task<List<FoydalanuvchiDto>?> Foydalanuvchilar() => Ol<List<FoydalanuvchiDto>>("foydalanuvchilar");
    public Task<List<FoydalanuvchiDto>?> Operatorlar() => Ol<List<FoydalanuvchiDto>>("operatorlar");
    public Task<FoydalanuvchiDto?> FoydalanuvchiYarat(FoydalanuvchiYaratishDto d) => Yubor<FoydalanuvchiDto>(HttpMethod.Post, "foydalanuvchilar", d);
    public Task<FoydalanuvchiDto?> FoydalanuvchiTahrirla(int id, FoydalanuvchiTahrirlashDto d) => Yubor<FoydalanuvchiDto>(HttpMethod.Put, $"foydalanuvchilar/{id}", d);
    public Task<FoydalanuvchiDto?> RuxsatlarniOrnat(int id, RuxsatlarOrnatishDto d) => Yubor<FoydalanuvchiDto>(HttpMethod.Put, $"foydalanuvchilar/{id}/ruxsatlar", d);
    public Task PinOrnat(int id, PinOrnatishDto d) => Yubor<object>(HttpMethod.Post, $"foydalanuvchilar/{id}/pin", d);

    // ---------- Smena / sotuv ----------
    public Task<List<SmenaDto>?> Smenalar(DateOnly? dan = null, int? operatorId = null) =>
        Ol<List<SmenaDto>>("smenalar" + Sorov(("dan", dan), ("operatorId", operatorId)));
    public Task<SmenaDto?> SmenaOch() => Yubor<SmenaDto>(HttpMethod.Post, "smenalar/och", null);
    public Task<SmenaDto?> SmenaYop(int id, SmenaYopishDto d) => Yubor<SmenaDto>(HttpMethod.Post, $"smenalar/{id}/yop", d);

    public Task<List<SotuvDto>?> Sotuvlar(DateOnly? dan = null, DateOnly? gacha = null, int? smenaId = null, SotuvHolati? holati = null) =>
        Ol<List<SotuvDto>>("sotuvlar" + Sorov(("dan", dan), ("gacha", gacha), ("smenaId", smenaId), ("holati", holati)));
    public Task<SotuvDto?> SotuvYarat(SotuvYaratishDto d) => Yubor<SotuvDto>(HttpMethod.Post, "sotuvlar", d);
    public Task<SotuvDto?> SotuvTahrirla(int id, SotuvTahrirlashDto d) => Yubor<SotuvDto>(HttpMethod.Put, $"sotuvlar/{id}", d);
    public Task<SotuvDto?> SotuvBekorQil(int id, SotuvBekorQilishDto d) => Yubor<SotuvDto>(HttpMethod.Post, $"sotuvlar/{id}/bekor", d);

    // ---------- Operator hisobi / audit / zaxira ----------
    public Task<OperatorHisobDto?> OperatorHisobi(int id) => Ol<OperatorHisobDto>($"operatorlar/{id}/hisob");
    public Task<HisobHarakatiDto?> HarakatYoz(int id, HarakatYaratishDto d) => Yubor<HisobHarakatiDto>(HttpMethod.Post, $"operatorlar/{id}/harakat", d);
    public Task<List<AuditYozuviDto>?> Audit(int limit) => Ol<List<AuditYozuviDto>>($"audit?limit={limit}");
    public Task AuditEksport(AuditEksportDto d) => Yubor<object>(HttpMethod.Post, "audit/eksport", d);
    public Task<ZaxiraJavobiDto?> Zaxira() => Yubor<ZaxiraJavobiDto>(HttpMethod.Post, "zaxira", null);

    // ---------- Boshqaruv / hisobot (serverda hisoblanadi) ----------
    public Task<BoshqaruvBugunDto?> Boshqaruv() => Ol<BoshqaruvBugunDto>("boshqaruv/bugun");
    public Task<HisobotDto?> Hisobot(DateOnly dan, DateOnly gacha, int? operatorId, HisobotGuruhi guruh) =>
        Ol<HisobotDto>("hisobot" + Sorov(("dan", dan), ("gacha", gacha), ("operatorId", operatorId), ("guruh", guruh)));

    // ---------- Ichki ----------
    private static string Sorov(params (string Nomi, object? Qiymat)[] p)
    {
        var qismlar = p.Where(x => x.Qiymat is not null).Select(x => x.Nomi + "=" + Uri.EscapeDataString(x.Qiymat switch
        {
            DateOnly d => d.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(x.Qiymat, System.Globalization.CultureInfo.InvariantCulture)!,
        })).ToList();
        return qismlar.Count == 0 ? "" : "?" + string.Join("&", qismlar);
    }

    private Task<T?> Ol<T>(string yol) => Yubor<T>(HttpMethod.Get, yol, null);

    private async Task<T?> Yubor<T>(HttpMethod usul, string yol, object? tana, bool tokenli = true)
    {
        using var sorov = new HttpRequestMessage(usul, yol);
        if (tana is not null) sorov.Content = JsonContent.Create(tana, tana.GetType(), options: Json);

        HttpResponseMessage javob;
        try
        {
            javob = await _http.SendAsync(sorov);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new ApiXatosi(Til.T("AloqaYoq"), 0);
        }

        using (javob)
        {
            if (javob.IsSuccessStatusCode)
            {
                if (javob.StatusCode == HttpStatusCode.NoContent || javob.Content.Headers.ContentLength == 0) return default;
                return await javob.Content.ReadFromJsonAsync<T>(Json);
            }

            if (javob.StatusCode == HttpStatusCode.Unauthorized && tokenli)
            {
                SessiyaTugadi?.Invoke();
                throw new ApiXatosi(Til.T("SessiyaTugadi"), 401);
            }

            string xabar;
            try
            {
                var p = await javob.Content.ReadFromJsonAsync<JsonElement>(Json);
                xabar = p.TryGetProperty("detail", out var d) && d.GetString() is { Length: > 0 } dt ? dt
                      : p.TryGetProperty("title", out var t) && t.GetString() is { Length: > 0 } tt ? tt
                      : Til.F("ServerXatosi", (int)javob.StatusCode);
            }
            catch (Exception)
            {
                xabar = Til.F("ServerXatosi", (int)javob.StatusCode);
            }
            throw new ApiXatosi(xabar, (int)javob.StatusCode);
        }
    }
}
