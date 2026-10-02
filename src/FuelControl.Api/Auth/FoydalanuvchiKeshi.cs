using System.Security.Claims;
using FuelControl.Api.Data;
using FuelControl.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FuelControl.Api.Auth;

/// <summary>
/// Token ichidagi ruxsatlar kirish paytidagi holat. Har so'rovda foydalanuvchining joriy holati (Faol, Ruxsatlar)
/// bazadan olinadi (30 s kesh; foydalanuvchi o'zgarganda kesh darhol tozalanadi): nofaol → 401,
/// olib tashlangan ruxsat → 403 (RuxsatTalabFilter yangilangan claim'larni tekshiradi).
/// </summary>
public sealed class FoydalanuvchiKeshi(IMemoryCache kesh, IServiceScopeFactory scopes)
{
    public sealed record Holat(bool Faol, string Ism, Ruxsat[] Ruxsatlar);

    private static string Kalit(int id) => $"foydalanuvchi:{id}";

    public async Task<Holat?> Ol(int id) =>
        await kesh.GetOrCreateAsync(Kalit(id), async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FuelControlDbContext>();
            var f = await db.Foydalanuvchilar.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return f is null ? null : new Holat(f.Faol, f.ToliqIsm, f.Ruxsatlar.Distinct().ToArray());
        });

    public void Unut(int id) => kesh.Remove(Kalit(id));

    /// <summary>JwtBearer OnTokenValidated: claim'larni bazadagi joriy holat bilan almashtiradi.</summary>
    public static async Task TokenniTekshir(TokenValidatedContext ctx)
    {
        var kesh = ctx.HttpContext.RequestServices.GetRequiredService<FoydalanuvchiKeshi>();
        var identity = (ClaimsIdentity)ctx.Principal!.Identity!;
        if (!int.TryParse(identity.FindFirst("sub")?.Value, out var id) || await kesh.Ol(id) is not { Faol: true } holat)
        {
            ctx.Fail("Foydalanuvchi topilmadi yoki nofaol.");
            return;
        }
        foreach (var c in identity.FindAll(x => x.Type is "ruxsat" or "ism").ToList()) identity.RemoveClaim(c);
        identity.AddClaim(new Claim("ism", holat.Ism));
        foreach (var r in holat.Ruxsatlar) identity.AddClaim(new Claim("ruxsat", r.ToString()));
    }
}
