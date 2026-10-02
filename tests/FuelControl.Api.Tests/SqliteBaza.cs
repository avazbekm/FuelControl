using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FuelControl.Api.Tests;

/// <summary>Har test uchun migratsiya qilingan in-memory SQLite: 1 operator, AI-92 (12 200), 1 aparat (1000 L).</summary>
public abstract class SqliteBaza : IDisposable
{
    private readonly SqliteConnection _ulanish = new("Data Source=:memory:");
    protected readonly int OperatorId, AparatId;

    protected SqliteBaza()
    {
        _ulanish.Open();
        using var db = Yangi();
        db.Database.Migrate();
        var yoqilgi = new YoqilgiTuri { Nomi = "AI-92", Narx = 12_200 };
        var op = new Foydalanuvchi { ToliqIsm = "Sardor", Login = "sardor", Rol = Rol.Operator };
        db.AddRange(yoqilgi, op);
        db.SaveChanges();
        var aparat = new Aparat { Raqam = 1, YoqilgiTuriId = yoqilgi.Id, TotalLitr = 1000m };
        db.Add(aparat);
        db.SaveChanges();
        (OperatorId, AparatId) = (op.Id, aparat.Id);
    }

    protected FuelControlDbContext Yangi(params IInterceptor[] interseptorlar) =>
        new(new DbContextOptionsBuilder<FuelControlDbContext>().UseSqlite(_ulanish).AddInterceptors(interseptorlar).Options);

    protected SotuvYaratishDto Sorov(Guid kalit) =>
        new(AparatId, null, 100_000, [new TolovDto(TolovTuri.Naqd, 100_000)], kalit);

    public void Dispose() => _ulanish.Dispose();
}
