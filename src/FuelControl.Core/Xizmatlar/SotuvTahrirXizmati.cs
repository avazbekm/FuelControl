using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

public static class SotuvTahrirXizmati
{
    /// <summary>Eski va yangi qiymatlar — chaqiruvchi (Api) shu delta asosida aparat totalizatori va smena yakunini qayta hisoblaydi (eski ayriladi, yangi qo'shiladi).</summary>
    public sealed record TahrirNatijasi(long EskiSumma, decimal EskiLitr, int EskiAparatId, long YangiSumma, decimal YangiLitr, int YangiAparatId);

    public static TahrirNatijasi Tahrirla(Sotuv sotuv, int yangiAparatId, long yangiNarx, long yangiSumma, List<SotuvTolovi> yangiTolovlar)
    {
        if (!sotuv.Faolmi) throw new InvalidOperationException("Bekor qilingan sotuvni tahrirlash mumkin emas.");

        var yigindisi = SotuvHisoblagich.TolovlarYigindisi(yangiTolovlar);
        if (yigindisi != yangiSumma) throw new ArgumentException("To'lovlar yig'indisi summaga teng bo'lishi kerak.");

        var eskiSumma = sotuv.Summa;
        var eskiLitr = sotuv.Litr;
        var eskiAparatId = sotuv.AparatId;

        sotuv.AparatId = yangiAparatId;
        sotuv.Narx = yangiNarx;
        sotuv.Summa = yangiSumma;
        sotuv.Litr = SotuvHisoblagich.SummadanLitr(yangiSumma, yangiNarx);
        sotuv.Tolovlar = yangiTolovlar;

        return new TahrirNatijasi(eskiSumma, eskiLitr, eskiAparatId, sotuv.Summa, sotuv.Litr, sotuv.AparatId);
    }

    /// <summary>Bekor qilish: o'chirilmaydi, holat o'zgaradi — eski summa/litr aparat totalizatori va smena yakunidan ayiriladi.</summary>
    public static void BekorQil(Sotuv sotuv, string sabab, string kim)
    {
        if (!sotuv.Faolmi) throw new InvalidOperationException("Sotuv allaqachon bekor qilingan.");
        sotuv.Holati = Contracts.SotuvHolati.BekorQilingan;
        sotuv.BekorSababi = sabab;
        sotuv.BekorQilgan = kim;
    }
}
