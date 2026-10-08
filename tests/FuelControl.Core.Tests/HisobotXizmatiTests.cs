using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class HisobotXizmatiTests
{
    private static readonly Dictionary<int, string> Ismlar = new() { [2] = "Alisher Karimov", [3] = "Dilshod Rahimov" };

    private static Smena S(int id, int op, DateTime boshlandi, long savdo, decimal litr, long plastik, long depozit, long nasiya, long qaytgan, long xarajat, long farq) => new()
    {
        Id = id, OperatorId = op, Boshlandi = boshlandi, Tugadi = boshlandi.AddHours(24),
        Savdo = savdo, JamiLitr = litr, Plastik = plastik, DepozitFarqi = depozit, NasiyaJami = nasiya, QaytganNasiya = qaytgan, XarajatJami = xarajat, Farq = farq,
    };

    /// <summary>Dizayndagi #39-#41: yig'indi Savdo 48 590 420, Plastik 22 030 000, Depozit 5 735 900, Nasiya 1 910 000, Naqd 18 914 520, farq -100 000.</summary>
    private static List<Smena> Uchta() =>
    [
        S(39, 3, new DateTime(2026, 10, 1, 3, 3, 0, DateTimeKind.Utc), 16_098_210, 1205.60m, 7_300_000, 3_465_900, 915_000, 300_000, 930_000, 20_000),
        S(40, 2, new DateTime(2026, 10, 2, 3, 1, 0, DateTimeKind.Utc), 16_496_940, 1236.10m, 7_400_000, 2_000_000, 900_000, 150_000, 1_000_000, -120_000),
        S(41, 3, new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc), 15_995_270, 1198.40m, 7_330_000, 270_000, 95_000, 0, 50_000, 0),
    ];

    [Fact]
    public void Jami_DizaynNamunasi_NaqdSavdoVaKamomatOrtiqcha()
    {
        var j = HisobotXizmati.Qator("Jami", null, null, Uchta(), jami: true);

        Assert.Equal((3, 3640.10m, 48_590_420L), (j.SmenaSoni, j.Litr, j.Savdo));
        Assert.Equal((22_030_000L, 5_735_900L, 1_910_000L, 450_000L, 1_980_000L), (j.Plastik, j.Depozit, j.Nasiya, j.QaytganNasiya, j.Xarajat));
        Assert.Equal(18_914_520, j.NaqdSavdo);                 // Savdo - Plastik - Depozit - Nasiya
        Assert.Equal((120_000L, 20_000L), (j.Kamomat, j.Ortiqcha));   // alohida yig'indi: sof farq -100 000
        Assert.True(j.Jami);
    }

    [Fact]
    public void SmenaBoyicha_YangisiTepada_GuruhSmenaRaqami_SanaVaOperator()
    {
        var q = HisobotXizmati.Qatorlar(HisobotGuruhi.Smena, Uchta(), Ismlar);

        Assert.Equal(["41", "40", "39"], q.Select(x => x.Guruh));
        Assert.Equal((new DateOnly(2026, 10, 3), "Dilshod Rahimov", 1), (q[0].Sana, q[0].OperatorIsmi, q[0].SmenaSoni));
        Assert.Equal((-0L, 120_000L), (q[0].Kamomat, q[1].Kamomat));
        Assert.All(q, x => Assert.False(x.Jami));
    }

    [Fact]
    public void KunBoyicha_SmenaOchilganToshkentSanasigaYoziladi()
    {
        var smenalar = Uchta();
        // 3-oktabr 20:00 UTC = 4-oktabr 01:00 Toshkent: 4-oktabrga yoziladi (kalendar kun emas, Toshkent sanasi).
        smenalar.Add(S(42, 2, new DateTime(2026, 10, 3, 20, 0, 0, DateTimeKind.Utc), 1000, 1m, 0, 0, 0, 0, 0, 0));

        var q = HisobotXizmati.Qatorlar(HisobotGuruhi.Kun, smenalar, Ismlar);

        Assert.Equal(["2026-10-04", "2026-10-03", "2026-10-02", "2026-10-01"], q.Select(x => x.Guruh));
        Assert.Equal(new DateOnly(2026, 10, 4), q[0].Sana);
        Assert.Null(q[0].OperatorIsmi);
        Assert.Equal(1000, q[0].Savdo);
    }

    [Fact]
    public void OyBoyicha_OyChegarasiToshkentVaqtida()
    {
        var smenalar = Uchta();
        smenalar.Add(S(38, 2, new DateTime(2026, 9, 30, 18, 0, 0, DateTimeKind.Utc), 500, 1m, 0, 0, 0, 0, 0, 0));   // 30-sentabr 23:00 Toshkent
        smenalar.Add(S(37, 2, new DateTime(2026, 9, 30, 19, 30, 0, DateTimeKind.Utc), 700, 1m, 0, 0, 0, 0, 0, 0)); // 1-oktabr 00:30 Toshkent

        var q = HisobotXizmati.Qatorlar(HisobotGuruhi.Oy, smenalar, Ismlar);

        Assert.Equal(["2026-10", "2026-09"], q.Select(x => x.Guruh));
        Assert.Equal((4, 500L), (q[0].SmenaSoni, q[1].Savdo));   // #37, #39, #40, #41 oktabrda; #38 sentabrda
        Assert.Null(q[0].Sana);
    }

    [Fact]
    public void OperatorBoyicha_IsmBoyichaTartib_BirXilIsmliOperatorlarAralashmaydi()
    {
        var q = HisobotXizmati.Qatorlar(HisobotGuruhi.Operator, Uchta(), Ismlar);
        Assert.Equal(["Alisher Karimov", "Dilshod Rahimov"], q.Select(x => x.Guruh));
        Assert.Equal((1, 2), (q[0].SmenaSoni, q[1].SmenaSoni));

        var bir = new Dictionary<int, string> { [2] = "Ali", [3] = "Ali" };
        var ikki = HisobotXizmati.Qatorlar(HisobotGuruhi.Operator, Uchta(), bir);
        Assert.Equal(2, ikki.Length);
    }

    [Fact]
    public void Tuz_QatorlarJamiAparatlarVaAvans()
    {
        var h = HisobotXizmati.Tuz(HisobotGuruhi.Smena, Uchta(), Ismlar, [], 1_000_000);
        Assert.Equal((3, "Jami", 1_000_000L), (h.Qatorlar.Length, h.Jami.Guruh, h.Avans));
        Assert.Empty(h.Aparatlar);
    }
}
