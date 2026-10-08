using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;

namespace FuelControl.Core.Tests;

/// <summary>Dizayndagi namuna (smena #42): 5 aparat, narxlar, ochilish qoldiqlari va smena davomidagi yig'indilar.</summary>
internal static class Namuna
{
    public const int Ai92 = 1, Ai95 = 2, Dizel = 3;

    public static readonly Dictionary<int, long> Narxlar = new() { [Ai92] = 12_200, [Ai95] = 15_500, [Dizel] = 13_800 };

    /// <summary>Oldingi pult ko'rsatkichi (oxirgi yopilgan smena holati) va bak qoldig'i (smena yopilishidan oldin).</summary>
    public static List<Aparat> Aparatlar() =>
    [
        new() { Id = 1, Raqam = 1, YoqilgiTuriId = Ai92, TotalLitr = 184_230.50m, BakQoldiq = 6_840m },
        new() { Id = 2, Raqam = 2, YoqilgiTuriId = Ai92, TotalLitr = 97_410.00m, BakQoldiq = 3_215m },
        new() { Id = 3, Raqam = 3, YoqilgiTuriId = Ai95, TotalLitr = 63_118.20m, BakQoldiq = 4_120m },
        new() { Id = 4, Raqam = 4, YoqilgiTuriId = Ai95, TotalLitr = 41_902.75m, BakQoldiq = 1_960m },
        new() { Id = 5, Raqam = 5, YoqilgiTuriId = Dizel, TotalLitr = 120_560.00m, BakQoldiq = 9_480m },
    ];

    public static Dictionary<int, decimal> YangiKorsatkichlar() => new()
    {
        [1] = 184_642.30m, [2] = 97_655.40m, [3] = 63_296.70m, [4] = 41_990.25m, [5] = 120_873.60m,
    };

    public static DateTime Vaqt(int soat = 12) => new(2026, 10, 4, soat, 0, 0, DateTimeKind.Utc);

    public static Smena OchiqSmena() => new()
    {
        Id = 42, OperatorId = 2, Boshlandi = Vaqt(3),
        OchishQaytim = 100_000, OchishTerminal = 200_000, OchishDepozit = 1_250_000,
    };

    /// <summary>Nasiya 570 000, qaytgan 300 000, xarajat 1 585 000.</summary>
    public static readonly SmenaHisoblagich.Yigindilar Yig = new(570_000, 300_000, 1_585_000);

    /// <summary>Namuna smenani yopadi: terminal 7 830 000, depozit 3 410 000, sanalgan naqd 4 878 000.</summary>
    public static SmenaHisoblagich.YopishNatijasi Yop(Smena smena, List<Aparat> aparatlar, long sanalgan = 4_878_000,
        IReadOnlyCollection<SmenaKorsatkichi>? mavjud = null, SmenaHisoblagich.Yigindilar? yig = null) =>
        SmenaHisoblagich.Yop(smena, aparatlar, Narxlar, mavjud ?? [], YangiKorsatkichlar(),
            7_830_000, 3_410_000, sanalgan, null, yig ?? Yig, Vaqt());
}
