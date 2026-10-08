using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>HisobotQatoriDto.XarajatSoni: qatordagi smenalarda yozilgan xarajatlar soni (Jami - hammasi).</summary>
public class XarajatSoniTests
{
    private static readonly Dictionary<int, string> Ismlar = new() { [2] = "Alisher Karimov", [3] = "Dilshod Rahimov" };

    private static Smena S(int id, int op, DateTime boshlandi, long xarajat) => new()
    {
        Id = id, OperatorId = op, Boshlandi = boshlandi, Tugadi = boshlandi.AddHours(24), XarajatJami = xarajat,
    };

    private static List<Smena> Uchta() =>
    [
        S(39, 3, new DateTime(2026, 10, 1, 3, 3, 0, DateTimeKind.Utc), 310_000),
        S(40, 2, new DateTime(2026, 10, 2, 3, 1, 0, DateTimeKind.Utc), 1_620_000),
        S(41, 3, new DateTime(2026, 10, 3, 3, 0, 0, DateTimeKind.Utc), 50_000),
    ];

    private static readonly Dictionary<int, int> Soni = new() { [39] = 2, [40] = 2, [41] = 1 };

    [Fact]
    public void Smena_Kun_Oy_Operator_QatorlarSmenalardagiXarajatlarSoniniYigadi()
    {
        Assert.Equal([1, 2, 2], HisobotXizmati.Qatorlar(HisobotGuruhi.Smena, Uchta(), Ismlar, Soni).Select(q => q.XarajatSoni));         // #41, #40, #39
        Assert.Equal([1, 2, 2], HisobotXizmati.Qatorlar(HisobotGuruhi.Kun, Uchta(), Ismlar, Soni).Select(q => q.XarajatSoni));
        Assert.Equal([5], HisobotXizmati.Qatorlar(HisobotGuruhi.Oy, Uchta(), Ismlar, Soni).Select(q => q.XarajatSoni));
        var operatorlar = HisobotXizmati.Qatorlar(HisobotGuruhi.Operator, Uchta(), Ismlar, Soni);
        Assert.Equal([("Alisher Karimov", 2), ("Dilshod Rahimov", 3)], operatorlar.Select(q => (q.Guruh, q.XarajatSoni)));
    }

    [Fact]
    public void Jami_HammaSmenalarSoni_BerilmasaNol()
    {
        var h = HisobotXizmati.Tuz(HisobotGuruhi.Smena, Uchta(), Ismlar, [], 0, Soni);
        Assert.Equal((5, 1_980_000L), (h.Jami.XarajatSoni, h.Jami.Xarajat));
        Assert.All(HisobotXizmati.Qatorlar(HisobotGuruhi.Smena, Uchta(), Ismlar), q => Assert.Equal(0, q.XarajatSoni));
        Assert.Equal(0, HisobotXizmati.Tuz(HisobotGuruhi.Smena, Uchta(), Ismlar, [], 0).Jami.XarajatSoni);
    }

    [Fact]
    public void KeyingiOyBoshi_ToshkentOyiChegarasi()
    {
        // 1-oktabr 00:00 Toshkent = 30-sentabr 19:00 UTC; keyingi oy - 1-noyabr 00:00 Toshkent = 31-oktabr 19:00 UTC.
        var utc = new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 9, 30, 19, 0, 0, DateTimeKind.Utc), Vaqt.OyBoshi(utc));
        Assert.Equal(new DateTime(2026, 10, 31, 19, 0, 0, DateTimeKind.Utc), Vaqt.KeyingiOyBoshi(utc));
        // 31-oktabr 20:00 UTC = 1-noyabr 01:00 Toshkent: allaqachon noyabr.
        Assert.Equal(new DateTime(2026, 11, 30, 19, 0, 0, DateTimeKind.Utc), Vaqt.KeyingiOyBoshi(new DateTime(2026, 10, 31, 20, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void NasiyaQidiruvi_IsmTelefonVaRaqamBoyicha()
    {
        var n = new Nasiya { MijozIsmi = "Bobur Aliyev", Telefon = "+998 97 700 80 90", MashinaRaqami = "01 H 202 MA" };
        Assert.True(NasiyaXizmati.QidiruvgaMos(n, null));
        Assert.True(NasiyaXizmati.QidiruvgaMos(n, "  "));
        Assert.True(NasiyaXizmati.QidiruvgaMos(n, "BOBUR"));
        Assert.True(NasiyaXizmati.QidiruvgaMos(n, "97 700"));
        Assert.True(NasiyaXizmati.QidiruvgaMos(n, "+998-97-700"));
        Assert.True(NasiyaXizmati.QidiruvgaMos(n, "01h202"));
        Assert.True(NasiyaXizmati.QidiruvgaMos(n, "h 202 ma"));
        Assert.False(NasiyaXizmati.QidiruvgaMos(n, "sherzod"));
        Assert.False(NasiyaXizmati.QidiruvgaMos(n, "999"));
        Assert.False(NasiyaXizmati.QidiruvgaMos(n, "---"));                                  // faqat belgilar - raqamga aylanmaydi
    }
}
