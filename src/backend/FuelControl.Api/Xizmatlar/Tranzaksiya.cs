using FuelControl.Api.Auth;
using FuelControl.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

/// <summary>
/// O'qish va yozishni bitta tranzaksiyada bajaradi. SQLite (WAL) da boshqa yozuvchi oraga kirsa, eskirgan o'qishdan keyingi yozish
/// SQLITE_BUSY(_SNAPSHOT) bilan rad etiladi: balansni ikki marta o'zgartirish (masalan, smenani parallel ikki marta yopish) o'rniga 409 qaytariladi.
/// </summary>
public static class Tranzaksiya
{
    public static async Task<T> Bajar<T>(FuelControlDbContext db, Func<Task<T>> ish)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var natija = await ish();
            await tx.CommitAsync();
            return natija;
        }
        catch (Exception e) when (Band(e))
        {
            throw new BiznesXatosi("Ma'lumot shu paytda boshqa foydalanuvchi tomonidan o'zgartirilmoqda. Qayta urinib ko'ring.", 409);
        }
    }

    public static Task Bajar(FuelControlDbContext db, Func<Task> ish) => Bajar(db, async () => { await ish(); return 0; });

    private static bool Band(Exception e) =>
        e is SqliteException { SqliteErrorCode: 5 or 6 } || e.InnerException is SqliteException { SqliteErrorCode: 5 or 6 };

    /// <summary>UNIQUE/PRIMARY KEY buzilishi (SQLite 19) — bir vaqtdagi ikkinchi yozuv.</summary>
    public static bool Takror(DbUpdateException e) => e.InnerException is SqliteException { SqliteErrorCode: 19 };
}
