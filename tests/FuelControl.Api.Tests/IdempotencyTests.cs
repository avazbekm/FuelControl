using FuelControl.Api.Xizmatlar;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FuelControl.Api.Tests;

public sealed class IdempotencyTests : SqliteBaza
{
    [Fact]
    public async Task ParallelSorovKalitniOldinYozgan_MavjudSotuvQaytadi_TakrorYozilmaydi()
    {
        var kalit = Guid.NewGuid();

        // Birinchi so'rov yozib bo'ldi.
        await using (var a = Yangi())
        {
            var (_, yangi) = await SotuvXizmati.Yarat(a, OperatorId, Sorov(kalit));
            Assert.True(yangi);
        }

        // Ikkinchi so'rov endpoint'dagi oldindan tekshiruvdan o'tib bo'lgan holat — to'g'ridan-to'g'ri qo'shishga uriniladi.
        await using (var b = Yangi())
        {
            var (sotuv, yangi) = await SotuvXizmati.Yarat(b, OperatorId, Sorov(kalit));
            Assert.False(yangi);
            Assert.Equal(kalit, sotuv.IdempotencyKey);
        }

        await using var t = Yangi();
        Assert.Equal(1, await t.Sotuvlar.CountAsync());
        var aparat = await t.Aparatlar.SingleAsync();
        Assert.Equal(1000m + 8.20m, aparat.TotalLitr);
        var smena = await t.Smenalar.SingleAsync();
        Assert.Equal(1, smena.SotuvSoni);
        Assert.Equal(100_000, smena.KutilganNaqd);
    }

    [Fact]
    public async Task BoshqaKalitlar_IkkalaSotuvYoziladi()
    {
        await using var db = Yangi();
        await SotuvXizmati.Yarat(db, OperatorId, Sorov(Guid.NewGuid()));
        await SotuvXizmati.Yarat(db, OperatorId, Sorov(Guid.NewGuid()));
        Assert.Equal(2, await db.Sotuvlar.CountAsync());
    }
}
