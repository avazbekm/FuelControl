using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

/// <summary>Sotuv summa/litr hisob-kitobi: "summa kiritilsa litr = summa/narx (2 xona), litr kiritilsa summa = litr×narx (so'mgacha)".</summary>
public static class SotuvHisoblagich
{
    public static decimal SummadanLitr(long summa, long narx)
    {
        if (narx <= 0) throw new ArgumentOutOfRangeException(nameof(narx), "Narx musbat bo'lishi kerak.");
        return Math.Round(summa / (decimal)narx, 2, MidpointRounding.AwayFromZero);
    }

    public static long LitrdanSumma(decimal litr, long narx)
    {
        if (narx <= 0) throw new ArgumentOutOfRangeException(nameof(narx), "Narx musbat bo'lishi kerak.");
        return (long)Math.Round(litr * narx, 0, MidpointRounding.AwayFromZero);
    }

    public static long TolovlarYigindisi(IEnumerable<SotuvTolovi> tolovlar) => tolovlar.Sum(t => t.Summa);
}
