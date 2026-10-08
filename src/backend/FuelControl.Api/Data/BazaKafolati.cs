using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Data;

/// <summary>
/// EF modeli ifodali indeksni bilmaydi: kelajakdagi migratsiya SQLite jadvalni qayta qursa u yo'qolishi mumkin.
/// Ilova har ishga tushganda kafolatni tiklab qo'yamiz (IF NOT EXISTS - mavjud bo'lsa hech narsa qilmaydi).
/// </summary>
public static class BazaKafolati
{
    /// <summary>Butun shoxobchada bir vaqtda faqat bitta ochiq smena (Tugadi IS NULL qatorlari orasida UNIQUE ((1))).</summary>
    public const string BittaOchiqSmenaSql =
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_Smenalar_BittaOchiq" ON "Smenalar" ((1)) WHERE "Tugadi" IS NULL;""";

    public static Task Tikla(FuelControlDbContext db) => db.Database.ExecuteSqlRawAsync(BittaOchiqSmenaSql);
}
