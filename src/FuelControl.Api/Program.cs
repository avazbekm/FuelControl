using System.Text.Json.Serialization;
using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using FuelControl.Api.Endpointlar;
using FuelControl.Api.Xizmatlar;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FuelControlDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Baza") ?? "Data Source=fuelcontrol.db"));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Web standarti sonlarni satrdan ham o'qiydi — OpenAPI'da "number | string" bo'lib chiqadi. Faqat son.
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Kalit yo'q bo'lsa — har so'rovda emas, ishga tushishdayoq to'xtaymiz.
var jwtKalit = TokenXizmati.Kalit(builder.Configuration);
if (jwtKalit.KeySize < 256) throw new InvalidOperationException("Jwt:Kalit kamida 32 belgi bo'lishi kerak.");

builder.Services.AddSingleton<TokenXizmati>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<FoydalanuvchiKeshi>();
builder.Services.AddSingleton<UlanishlarXaritasi>();
builder.Services.AddSingleton<ZaxiraXizmati>();
builder.Services.AddHostedService<KunlikIshlarFonXizmati>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = TokenXizmati.Emitent,
        ValidAudience = TokenXizmati.Emitent,
        IssuerSigningKey = jwtKalit,
        ClockSkew = TimeSpan.FromMinutes(1),
        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
    };
    // SignalR (WebSocket) tokenni so'rov satridan yuboradi.
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hub"))
                ctx.Token = token;
            return Task.CompletedTask;
        },
        OnTokenValidated = FoydalanuvchiKeshi.TokenniTekshir,
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler(h => h.Run(async ctx =>
{
    var xato = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, xabar) = xato switch
    {
        BiznesXatosi b => (b.Status, b.Message),
        ArgumentException a => (400, a.Message),
        InvalidOperationException i => (409, i.Message),
        _ => (500, "Serverda kutilmagan xato yuz berdi."),
    };
    if (status == 500) app.Logger.LogError(xato, "Kutilmagan xato");
    ctx.Response.StatusCode = status;
    await Results.Problem(statusCode: status, title: status == 500 ? "Server xatosi" : "So'rov bajarilmadi", detail: xabar)
        .ExecuteAsync(ctx);
}));
app.UseStatusCodePages();

// PWA (src/FuelControl.Web build → wwwroot). Hash marshrutlash — SPA fallback kerak emas.
var turlar = new FileExtensionContentTypeProvider();
turlar.Mappings[".webmanifest"] = "application/manifest+json";
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = turlar });

app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference().AllowAnonymous();

app.Ulash();
app.YoqilgiAparatUlash();
app.SmenaSotuvUlash();
app.HisobotUlash();
app.Services.GetRequiredService<ZaxiraXizmati>().Ulash(app);
app.MapHub<SotuvHub>("/hub").RequireAuthorization();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FuelControlDbContext>();
    db.Database.Migrate();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await SeedXizmati.Boshlash(db, builder.Configuration, app.Logger);
    if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:DemoMalumot"))
        await SeedXizmati.DemoMalumot(db, app.Logger);
    await MaoshYozuvchi.Yoz(db);
}

app.Run();

public partial class Program;
