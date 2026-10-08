using FuelControl.Contracts;
using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

public static class XarajatXizmati
{
    public const int MaksSabab = 200;

    /// <summary>Smena davomidagi xarajat: summa musbat, sabab majburiy. Manba Kassa yoki Depozit (smena hisobi uchun ikkalasi ham xarajat).</summary>
    public static Xarajat Yarat(int smenaId, int operatorId, string kim, long summa, string? sabab, XarajatManbai manba, DateTime vaqtUtc)
    {
        if (summa <= 0) throw new ArgumentException("Xarajat summasi musbat bo'lishi kerak.");
        if (string.IsNullOrWhiteSpace(sabab)) throw new ArgumentException("Xarajat sababi kiritilishi shart.");
        if (sabab.Trim().Length > MaksSabab) throw new ArgumentException($"Xarajat sababi {MaksSabab} belgidan oshmasligi kerak.");
        if (!Enum.IsDefined(manba)) throw new ArgumentException("Xarajat manbasi noto'g'ri.");
        return new Xarajat
        {
            SmenaId = smenaId, OperatorId = operatorId, KimYozdi = kim, Summa = summa, Sabab = sabab.Trim(), Manba = manba, Vaqt = vaqtUtc,
        };
    }
}
