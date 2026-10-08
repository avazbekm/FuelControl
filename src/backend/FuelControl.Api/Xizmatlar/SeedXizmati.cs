using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

public static class SeedXizmati
{
    /// <summary>
    /// Birinchi ishga tushishda admin foydalanuvchini yaratadi. yoqilgiVaAparatlar = true bo'lsa (Development/Web/Testing) bo'sh bazaga
    /// 3 ta namunaviy yoqilg'i va 5 ta aparat ham yoziladi; Production'da false: mijoz bazasida faqat admin bo'ladi, yoqilg'i va aparatlarni
    /// (haqiqiy boshlang'ich pult ko'rsatkichlari bilan) admin Sozlamalar'da o'zi kiritadi — namunaviy aparatlarni o'chirib bo'lmaydi va
    /// ularning TotalLitr=0 qiymati haqiqiy pultga mos kelmaydi.
    /// </summary>
    public static async Task Boshlash(FuelControlDbContext db, IConfiguration konf, ILogger log, bool yoqilgiVaAparatlar = true)
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

        if (yoqilgiVaAparatlar && !await db.Yoqilgilar.AnyAsync())
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
    /// Faqat Development + "Seed:DemoMalumot=true": desktop/web'ni qo'lda sinash uchun 2 operator (alisher/1234, dilshod/1234).
    /// VAQTINCHA (B1): to'liq demo (smenalar, nasiyalar, xarajatlar, bak kirimlari) B3 da qaytadi.
    /// </summary>
    public static async Task DemoMalumot(FuelControlDbContext db, ILogger log)
    {
        await Xizmatlar.DemoMalumot.Yoz(db, log);
    }
}
