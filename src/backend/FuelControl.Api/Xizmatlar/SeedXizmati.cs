using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

public static class SeedXizmati
{
    public static async Task Boshlash(FuelControlDbContext db, IConfiguration konf, ILogger log)
    {
        if (!await db.Foydalanuvchilar.AnyAsync())
        {
            var parol = konf["Seed:AdminParol"];
            if (string.IsNullOrWhiteSpace(parol))
                throw new InvalidOperationException("Birinchi ishga tushirishda Seed__AdminParol muhit o'zgaruvchisi kerak.");
            db.Foydalanuvchilar.Add(new Foydalanuvchi
            {
                ToliqIsm = "Administrator", Login = "admin", Rol = Rol.Admin,
                ParolXeshi = ParolXeshlash.Xeshla(parol), Ruxsatlar = RuxsatXizmati.Standart(Rol.Admin).ToList(),
            });
            log.LogInformation("Admin foydalanuvchi yaratildi (login: admin).");
        }

        if (!await db.Yoqilgilar.AnyAsync())
        {
            db.Yoqilgilar.AddRange(
                new YoqilgiTuri { Nomi = "AI-92", Narx = 12_200, Rang = "#2563EB" },
                new YoqilgiTuri { Nomi = "AI-95", Narx = 15_500, Rang = "#7C3AED" },
                new YoqilgiTuri { Nomi = "Dizel", Narx = 13_800, Rang = "#CA8A04" });
            await db.SaveChangesAsync();

            var id = await db.Yoqilgilar.ToDictionaryAsync(y => y.Nomi, y => y.Id);
            db.Aparatlar.AddRange(
                new Aparat { Raqam = 1, YoqilgiTuriId = id["AI-92"] },
                new Aparat { Raqam = 2, YoqilgiTuriId = id["AI-92"] },
                new Aparat { Raqam = 3, YoqilgiTuriId = id["AI-95"] },
                new Aparat { Raqam = 4, YoqilgiTuriId = id["AI-95"] },
                new Aparat { Raqam = 5, YoqilgiTuriId = id["Dizel"] });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Faqat Development + "Seed:DemoMalumot=true": desktop'ni qo'lda sinash uchun 2 operator (alisher/1234, dilshod/1234)
    /// va bugungi 6 ta sotuv. Kalitlar sanaga bog'liq va deterministik — qayta ishga tushganda takrorlanmaydi.
    /// </summary>
    public static async Task DemoMalumot(FuelControlDbContext db, ILogger log)
    {
        foreach (var (ism, login) in new[] { ("Alisher Karimov", "alisher"), ("Dilshod Rahimov", "dilshod") })
        {
            if (await db.Foydalanuvchilar.AnyAsync(f => f.Login == login)) continue;
            db.Foydalanuvchilar.Add(new Foydalanuvchi
            {
                ToliqIsm = ism, Login = login, Rol = Rol.Operator, OylikMaosh = 4_500_000,
                ParolXeshi = ParolXeshlash.Xeshla("1234"), Ruxsatlar = RuxsatXizmati.Standart(Rol.Operator).ToList(),
            });
        }
        await db.SaveChangesAsync();

        var bugun = DateTime.UtcNow.Add(Vaqt.Toshkent).ToString("yyyyMMdd");
        Guid Kalit(int n) => new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"demo-{bugun}-{n}")));
        var birinchi = Kalit(1);
        if (await db.Sotuvlar.AnyAsync(s => s.IdempotencyKey == birinchi)) return;

        var op = await db.Foydalanuvchilar.Where(f => f.Login == "alisher" || f.Login == "dilshod").ToDictionaryAsync(f => f.Login, f => f.Id);
        var aparatlar = await db.Aparatlar.OrderBy(a => a.Raqam).Select(a => a.Id).ToListAsync();
        if (aparatlar.Count == 0) return;
        int Ap(int i) => aparatlar[i % aparatlar.Count];

        var sotuvlar = new (string Op, SotuvYaratishDto Sorov)[]
        {
            ("alisher", new(Ap(0), null, 100_000, null, Kalit(1), TolovTuri.Naqd)),
            ("alisher", new(Ap(2), 20m, null, null, Kalit(2), TolovTuri.Plastik)),
            ("alisher", new(Ap(1), null, 50_000, null, Kalit(3), TolovTuri.Click)),
            ("dilshod", new(Ap(4), 15m, null, null, Kalit(4), TolovTuri.Naqd)),
            ("dilshod", new(Ap(0), null, 200_000, [new TolovDto(TolovTuri.Naqd, 120_000), new TolovDto(TolovTuri.Plastik, 80_000)], Kalit(5))),
            ("dilshod", new(Ap(3), null, 300_000, null, Kalit(6), TolovTuri.Click)),
        };
        foreach (var (o, s) in sotuvlar)
            await SotuvXizmati.Yarat(db, op[o], s);
        log.LogInformation("Demo ma'lumot: 2 operator va bugungi {Soni} ta sotuv yozildi.", sotuvlar.Length);
    }
}
