using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

/// <summary>SQLite `VACUUM INTO` bilan zaxira nusxa; 30 kundan eskilari o'chiriladi.</summary>
public sealed class ZaxiraXizmati(IConfiguration konfiguratsiya, IServiceScopeFactory scopes, ILogger<ZaxiraXizmati> log)
{
    private string Papka => konfiguratsiya["Zaxira:Papka"] ?? "zaxira";

    public async Task<ZaxiraJavobiDto> NusxaOl(CancellationToken ct = default)
    {
        Directory.CreateDirectory(Papka);
        var vaqt = DateTime.UtcNow;
        var fayl = Path.GetFullPath(Path.Combine(Papka, $"fuelcontrol-{vaqt:yyyyMMdd-HHmmss}.db"));

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FuelControlDbContext>();
        var ulanish = (SqliteConnection)db.Database.GetDbConnection();
        await ulanish.OpenAsync(ct);
        try
        {
            // Fayl nomi serverda yaratiladi (foydalanuvchi kiritmaydi); apostrof ekranlanadi.
            await using var buyruq = ulanish.CreateCommand();
            buyruq.CommandText = $"VACUUM INTO '{fayl.Replace("'", "''")}'";
            await buyruq.ExecuteNonQueryAsync(ct);
        }
        finally { await ulanish.CloseAsync(); }

        Tozala();
        return new ZaxiraJavobiDto(Path.GetFileName(fayl), new FileInfo(fayl).Length, vaqt);
    }

    private void Tozala()
    {
        var chegara = DateTime.UtcNow.AddDays(-30);
        foreach (var f in Directory.EnumerateFiles(Papka, "fuelcontrol-*.db"))
            if (File.GetLastWriteTimeUtc(f) < chegara) File.Delete(f);
    }

    public bool BugunNusxaBormi() =>
        Directory.Exists(Papka) && Directory.EnumerateFiles(Papka, $"fuelcontrol-{DateTime.UtcNow:yyyyMMdd}-*.db").Any();

    public void Ulash(IEndpointRouteBuilder app) =>
        app.MapPost("/zaxira", async (HttpContext ctx, ZaxiraXizmati z) =>
        {
            var natija = await z.NusxaOl(ctx.RequestAborted);
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FuelControlDbContext>();
            Audit.Yoz(db, ctx.User.Ism(), "Zaxira nusxa olindi", natija.FaylNomi);
            await db.SaveChangesAsync();
            return Results.Ok(natija);
        }).RuxsatKerak(Ruxsat.Sozlamalar).RequireAuthorization().Produces<ZaxiraJavobiDto>();

    internal ILogger Log => log;
}

/// <summary>Kuniga bir marta zaxira nusxa oladi (soatiga bir tekshiradi) va yangi oy uchun maoshlarni yozadi.</summary>
public sealed class KunlikIshlarFonXizmati(ZaxiraXizmati zaxira, IServiceScopeFactory scopes, ILogger<KunlikIshlarFonXizmati> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var taymer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                if (!zaxira.BugunNusxaBormi()) await zaxira.NusxaOl(ct);
                using var scope = scopes.CreateScope();
                await MaoshYozuvchi.Yoz(scope.ServiceProvider.GetRequiredService<FuelControlDbContext>());
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "Kunlik ish bajarilmadi");
            }
        } while (await taymer.WaitForNextTickAsync(ct));
    }
}
