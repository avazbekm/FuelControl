using FuelControl.Api.Data;
using FuelControl.Api.Xizmatlar;
using FuelControl.Core.Modellar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace FuelControl.Api.Tests;

public sealed class OchiqSmenaTests : SqliteBaza
{
    /// <summary>Kontekst smena qo'shmoqchi bo'lganda, undan oldinroq boshqa "so'rov" shu operatorga smena ochib qo'yadi.</summary>
    private sealed class RaqibSmenaOchadi(Func<FuelControlDbContext> yangi, int operatorId) : SaveChangesInterceptor
    {
        public int? RaqibSmenaId { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData e, InterceptionResult<int> natija, CancellationToken ct = default)
        {
            var smenaQoshilmoqda = e.Context!.ChangeTracker.Entries<Smena>().Any(x => x.State == EntityState.Added);
            if (smenaQoshilmoqda && RaqibSmenaId is null)
            {
                await using var raqib = yangi();
                var s = new Smena { OperatorId = operatorId, Boshlandi = DateTime.UtcNow };
                raqib.Smenalar.Add(s);
                await raqib.SaveChangesAsync(ct);
                RaqibSmenaId = s.Id;
            }
            return natija;
        }
    }

    [Fact]
    public async Task BazaIkkinchiOchiqSmenaniRadEtadi()
    {
        await using var db = Yangi();
        db.Smenalar.Add(new Smena { OperatorId = OperatorId, Boshlandi = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.Smenalar.Add(new Smena { OperatorId = OperatorId, Boshlandi = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task YopilganSmenalarCheklanmaydi()
    {
        await using var db = Yangi();
        db.Smenalar.Add(new Smena { OperatorId = OperatorId, Boshlandi = DateTime.UtcNow, Tugadi = DateTime.UtcNow });
        db.Smenalar.Add(new Smena { OperatorId = OperatorId, Boshlandi = DateTime.UtcNow, Tugadi = DateTime.UtcNow });
        db.Smenalar.Add(new Smena { OperatorId = OperatorId, Boshlandi = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.Equal(3, await db.Smenalar.CountAsync());
    }

    [Fact]
    public async Task BirinchiSotuvdaPoyga_RaqibOchganSmenagaYoziladi()
    {
        var raqib = new RaqibSmenaOchadi(() => Yangi(), OperatorId);
        await using (var db = Yangi(raqib))
        {
            var (sotuv, yangi) = await SotuvXizmati.Yarat(db, OperatorId, Sorov(Guid.NewGuid()));
            Assert.True(yangi);
            Assert.Equal(raqib.RaqibSmenaId, sotuv.SmenaId);
        }

        await using var t = Yangi();
        var smena = await t.Smenalar.SingleAsync();
        Assert.Null(smena.Tugadi);
        Assert.Equal(1, smena.SotuvSoni);
        Assert.Equal(1000m + 8.20m, (await t.Aparatlar.SingleAsync()).TotalLitr);
        // Muvaffaqiyatsiz urinishning "Smena ochildi" audit yozuvi saqlanmagan.
        Assert.Empty(await t.Audit.ToListAsync());
    }

    [Fact]
    public async Task QolBilanOchishdaPoyga_MavjudSmenaQaytadi()
    {
        var raqib = new RaqibSmenaOchadi(() => Yangi(), OperatorId);
        await using var db = Yangi(raqib);
        var (smena, yangi) = await SotuvXizmati.SmenaOch(db, OperatorId, "");
        Assert.False(yangi);
        Assert.Equal(raqib.RaqibSmenaId, smena.Id);
    }
}
