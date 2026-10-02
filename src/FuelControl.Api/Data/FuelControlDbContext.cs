using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FuelControl.Api.Data;

public sealed class UtcVaqtKonverteri() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

public sealed class FuelControlDbContext(DbContextOptions<FuelControlDbContext> options) : DbContext(options)
{
    public DbSet<Foydalanuvchi> Foydalanuvchilar => Set<Foydalanuvchi>();
    public DbSet<YoqilgiTuri> Yoqilgilar => Set<YoqilgiTuri>();
    public DbSet<NarxTarixi> NarxTarixlari => Set<NarxTarixi>();
    public DbSet<Aparat> Aparatlar => Set<Aparat>();
    public DbSet<Smena> Smenalar => Set<Smena>();
    public DbSet<Sotuv> Sotuvlar => Set<Sotuv>();
    public DbSet<SotuvTolovi> SotuvTolovlari => Set<SotuvTolovi>();
    public DbSet<HisobHarakati> Harakatlar => Set<HisobHarakati>();
    public DbSet<AuditYozuvi> Audit => Set<AuditYozuvi>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<DateTime>().HaveConversion<UtcVaqtKonverteri>();
        b.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        var ruxsatKonverter = new ValueConverter<List<Ruxsat>, string>(
            v => string.Join(',', v),
            v => string.IsNullOrEmpty(v)
                ? new List<Ruxsat>()
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<Ruxsat>).ToList());
        var ruxsatTaqqoslagich = new ValueComparer<List<Ruxsat>>(
            (a, b) => a!.SequenceEqual(b!),
            v => v.Aggregate(0, (h, x) => HashCode.Combine(h, (int)x)),
            v => v.ToList());

        m.Entity<Foydalanuvchi>(e =>
        {
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.Rol).HasConversion<string>();
            e.Property(x => x.Ruxsatlar).HasConversion(ruxsatKonverter, ruxsatTaqqoslagich);
        });

        m.Entity<YoqilgiTuri>(e => e.HasIndex(x => x.Nomi).IsUnique());

        m.Entity<NarxTarixi>(e => e.HasIndex(x => x.Vaqt));

        m.Entity<Aparat>(e =>
        {
            e.HasIndex(x => x.Raqam).IsUnique();
            e.HasOne<YoqilgiTuri>().WithMany().HasForeignKey(x => x.YoqilgiTuriId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<Smena>(e =>
        {
            e.Ignore(x => x.Ochiqmi);
            e.HasIndex(x => new { x.OperatorId, x.Tugadi });
            // Bir operatorda bir vaqtda faqat bitta ochiq smena — parallel so'rovlarda ham baza kafolatlaydi.
            e.HasIndex(x => x.OperatorId).IsUnique().HasFilter("\"Tugadi\" IS NULL").HasDatabaseName("IX_Smenalar_OchiqSmena");
            e.HasIndex(x => x.Boshlandi);
            e.HasOne<Foydalanuvchi>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<Sotuv>(e =>
        {
            e.Ignore(x => x.Faolmi);
            e.Property(x => x.Holati).HasConversion<string>();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => x.Vaqt);
            e.HasIndex(x => x.SmenaId);
            e.HasOne<Smena>().WithMany().HasForeignKey(x => x.SmenaId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Foydalanuvchi>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Aparat>().WithMany().HasForeignKey(x => x.AparatId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Tolovlar).WithOne().HasForeignKey(x => x.SotuvId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<SotuvTolovi>(e => e.Property(x => x.Turi).HasConversion<string>());

        m.Entity<HisobHarakati>(e =>
        {
            e.Property(x => x.Turi).HasConversion<string>();
            e.HasIndex(x => new { x.OperatorId, x.Sana });
            // Bir operatorga bir oyda bitta maosh — bir nechta API nusxasi/so'rov parallel yozsa ham.
            e.HasIndex(x => new { x.OperatorId, x.MaoshOyi }).IsUnique().HasFilter("\"MaoshOyi\" IS NOT NULL").HasDatabaseName("IX_Harakatlar_MaoshOyi");
            e.HasOne<Foydalanuvchi>().WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<AuditYozuvi>(e => e.HasIndex(x => x.Vaqt));
    }
}
