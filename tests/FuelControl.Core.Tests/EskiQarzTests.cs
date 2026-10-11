using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>Spec 8.11-8.12: eski qarz to'lovi (dasturdan oldingi qarz) va operator bo'yicha nasiya xulosasi.</summary>
public class EskiQarzTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 4);
    private static readonly DateTime OyBoshi = new(2026, 9, 30, 19, 0, 0, DateTimeKind.Utc);       // 1 oktabr 00:00 Toshkent
    private static readonly DateTime KeyingiOy = new(2026, 10, 31, 19, 0, 0, DateTimeKind.Utc);    // 1 noyabr 00:00 Toshkent

    private static (Nasiya N, NasiyaQaytishi Q) Eski(long summa = 300_000, TolovTuri usul = TolovTuri.Naqd, int smena = 42, int op = 2,
        string? ism = "  Bobur Aliyev ", string? tel = " 97 700 80 90 ", string? raqam = " 01 H 202 MA ", string? izoh = " eski qarz ", string kim = "Alisher") =>
        NasiyaXizmati.EskiQarz(smena, op, kim, ism, tel, raqam, summa, usul, izoh, Namuna.Vaqt());

    private static Nasiya Oddiy(int id, int op, string ism, long summa, long qaytgan = 0, string kim = "Qaxxor", int soat = 12)
    {
        var n = NasiyaXizmati.Yarat(42, op, kim, ism, "901234567", null, summa, Bugun, null, Namuna.Vaqt(soat));
        n.Id = id;
        if (qaytgan > 0) NasiyaXizmati.Qaytish(n, qaytgan, TolovTuri.Naqd, 42, op, kim, null, Namuna.Vaqt(soat));
        return n;
    }

    private static Nasiya EskiNasiya(int id, int op, string ism, long summa, string kim = "Qaxxor")
    {
        var (n, _) = Eski(summa, op: op, ism: ism, kim: kim);
        n.Id = id;
        return n;
    }

    [Fact]
    public void EskiQarz_NasiyaDarholYopilgan_QaytishiSmenaHisobigaYoziladi()
    {
        var (n, q) = Eski();

        Assert.True(n.EskiQarz);
        Assert.Equal((42, 2, "Alisher", "Bobur Aliyev", "+998 97 700 80 90", "01 H 202 MA", "eski qarz"),
            (n.SmenaId, n.OperatorId, n.KimYozdi, n.MijozIsmi, n.Telefon, n.MashinaRaqami, n.Izoh));
        Assert.Equal((300_000L, 300_000L, 0L, (DateTime?)Namuna.Vaqt()), (n.Summa, n.Qaytgan, n.Qoldiq, n.Yopildi));
        Assert.Equal((Bugun, Namuna.Vaqt()), (n.Muddat, n.Yozildi));
        Assert.Equal(NasiyaHolati.Yopilgan, NasiyaXizmati.Holat(n, Bugun));
        Assert.Equal(((int?)42, 300_000L, TolovTuri.Naqd, 2, "Alisher", "eski qarz", Namuna.Vaqt()), (q.SmenaId, q.Summa, q.Usul, q.OperatorId, q.KimYozdi, q.Izoh, q.Vaqt));
    }

    [Fact]
    public void EskiQarz_Muddat_ToshkentSanasi_UTCEmas()
    {
        // 4-oktabr 22:00 UTC = 5-oktabr 03:00 Toshkent.
        var (n, _) = NasiyaXizmati.EskiQarz(1, 1, "Op", "Ali", null, null, 1000, TolovTuri.Plastik, null, Namuna.Vaqt(22));
        Assert.Equal(new DateOnly(2026, 10, 5), n.Muddat);
    }

    [Fact]
    public void EskiQarz_TelefonVaMashinaRaqamiIxtiyoriy()
    {
        var yoq = NasiyaXizmati.EskiQarz(1, 1, "Op", "Ali", null, null, 1000, TolovTuri.Naqd, "  ", Namuna.Vaqt());
        Assert.Equal(("", "", null, null), (yoq.Nasiya.Telefon, yoq.Nasiya.MashinaRaqami, yoq.Nasiya.Izoh, yoq.Qaytish.Izoh));
        var bosh = NasiyaXizmati.EskiQarz(1, 1, "Op", "Ali", "   ", " ", 1000, TolovTuri.Naqd, null, Namuna.Vaqt());
        Assert.Equal(("", ""), (bosh.Nasiya.Telefon, bosh.Nasiya.MashinaRaqami));
        var tel = NasiyaXizmati.EskiQarz(1, 1, "Op", "Ali", "+998-90-123-45-67", null, 1000, TolovTuri.Naqd, null, Namuna.Vaqt());
        Assert.Equal("+998 90 123 45 67", tel.Nasiya.Telefon);
        Assert.Equal("+998 90 123 45 67", NasiyaXizmati.EskiQarz(1, 1, "Op", "Ali", "998901234567", null, 1000, TolovTuri.Naqd, null, Namuna.Vaqt()).Nasiya.Telefon);
    }

    [Fact]
    public void EskiQarz_Validatsiya_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Eski(ism: " "));
        Assert.Throws<ArgumentException>(() => Eski(ism: null));
        Assert.Throws<ArgumentException>(() => Eski(summa: 0));
        Assert.Throws<ArgumentException>(() => Eski(summa: -5));
        Assert.Throws<ArgumentException>(() => Eski(usul: (TolovTuri)99));
        Assert.Throws<ArgumentException>(() => Eski(tel: "90 123 45 6"));                // 8 raqam
        Assert.Throws<ArgumentException>(() => Eski(tel: "abc"));
        Assert.Throws<ArgumentException>(() => Eski(ism: new string('a', 101)));
        Assert.Throws<ArgumentException>(() => Eski(raqam: new string('1', 21)));
        Assert.Throws<ArgumentException>(() => Eski(izoh: new string('x', 501)));
    }

    [Fact]
    public void Yigindilar_EskiQarzNasiyasiNasiyaJamigaKirmaydi_QaytishiQaytganNasiyagaKiradi()
    {
        var (eskiN, eskiQ) = Eski(300_000);
        var boshqaSmena = Eski(777_000, smena: 7);
        var oddiy = Oddiy(1, 2, "Vali", 50_000);

        var y = SmenaHisoblagich.SmenaYigindilari(42, [eskiN, oddiy, boshqaSmena.N], [eskiQ, boshqaSmena.Q], []);

        Assert.Equal((50_000L, 300_000L, 0L), (y.NasiyaJami, y.QaytganNasiya, y.XarajatJami));
    }

    [Theory]
    [InlineData(TolovTuri.Naqd, 0L, 0L, 400_000L)]            // pul kassaga tushdi: kutilgan naqd 100 000 + 300 000
    [InlineData(TolovTuri.Plastik, 300_000L, 0L, 100_000L)]   // pul terminalga tushdi: plastik +300 000, naqd o'zgarmaydi
    [InlineData(TolovTuri.Depozit, 0L, 300_000L, 100_000L)]   // pul depozit kartaga tushdi: depozit farqi +300 000
    public void Kutilgan_EskiQarz_UsulgaQarabMuvozanatlanadi_NasiyaJamiOzgarmaydi(TolovTuri usul, long terminalOrtdi, long depozitOrtdi, long kutilganNaqd)
    {
        var smena = Namuna.OchiqSmena();                       // qaytim 100 000, terminal 200 000, depozit 1 250 000
        var (n, q) = Eski(300_000, usul);
        var y = SmenaHisoblagich.SmenaYigindilari(smena.Id, [n], [q], []);

        var natija = SmenaHisoblagich.Hisobla(smena, smena.OchishTerminal + terminalOrtdi, smena.OchishDepozit + depozitOrtdi, kutilganNaqd, [], y);

        Assert.Equal((0L, 300_000L, kutilganNaqd, 0L), (natija.NasiyaJami, natija.QaytganNasiya, natija.Kutilgan, natija.Farq));
        Assert.Equal((terminalOrtdi, depozitOrtdi), (natija.Plastik, natija.DepozitFarqi));
    }

    [Fact]
    public void Xulosa_OyBerilgan_EskiQarzniHisoblamaydi_OyQaytganHisoblaydi()
    {
        var oddiy = Oddiy(1, 2, "Vali", 50_000);
        var (eskiN, eskiQ) = Eski(300_000);
        eskiN.Id = 2;
        eskiQ.NasiyaId = 2;

        var x = NasiyaXizmati.Xulosa([oddiy, eskiN], [eskiQ], Bugun, OyBoshi, KeyingiOy);

        Assert.Equal((50_000L, 1, 300_000L, 1), (x.OyBerilgan, x.OyBerilganSoni, x.OyQaytgan, x.OyQaytganSoni));
        Assert.Equal((50_000L, 1, 0L, 0), (x.FaolQarz, x.FaolSoni, x.MuddatiOtgan, x.MuddatiOtganSoni));      // eski qarz yopilgan: faol emas
    }

    /// <summary>Operator 2 (Qaxxor): oddiy 100 000 (40 000 qaytgan), oddiy 50 000 (to'liq qaytgan), eski qarz 300 000 va 120 000. Operator 3: oddiy 70 000, eski 10 000.</summary>
    private static List<Nasiya> Toplam() =>
    [
        Oddiy(1, 2, "Bobur Aliyev", 100_000, 40_000, soat: 8), Oddiy(2, 2, "Vali", 50_000, 50_000, soat: 9),
        EskiNasiya(3, 2, "Bobur Aliyev", 300_000), EskiNasiya(4, 2, "Hamid", 120_000),
        Oddiy(5, 3, "Bobur Aliyev", 70_000, kim: "Sardor"), EskiNasiya(6, 3, "Sher", 10_000, kim: "Sardor"),
    ];

    [Fact]
    public void OperatorXulosa_EskiQarzFaqatEskiQarzQabulda()
    {
        var x = NasiyaXizmati.OperatorXulosa(Toplam(), 2, "Qaxxorjon");

        Assert.Equal(new NasiyaOperatorXulosaDto(2, "Qaxxorjon", Soni: 2, Berilgan: 150_000, Qaytgan: 90_000, Qoldiq: 60_000, FaolSoni: 1, EskiQarzQabul: 420_000), x);
        Assert.Equal(new NasiyaOperatorXulosaDto(3, "Sardor", 1, 70_000, 0, 70_000, 1, 10_000), NasiyaXizmati.OperatorXulosa(Toplam(), 3));   // ism nasiyalardan
    }

    [Fact]
    public void OperatorXulosa_IsmBermasa_EngYangiNasiyadagiIsm_TopilmasaBosh()
    {
        var nasiyalar = new List<Nasiya> { Oddiy(1, 2, "A", 1000, kim: "Eski Ism", soat: 8), Oddiy(2, 2, "B", 1000, kim: "Yangi Ism", soat: 15) };
        Assert.Equal("Yangi Ism", NasiyaXizmati.OperatorXulosa(nasiyalar, 2, "  ").OperatorIsmi);
        var yoq = NasiyaXizmati.OperatorXulosa(nasiyalar, 99);
        Assert.Equal(new NasiyaOperatorXulosaDto(99, "", 0, 0, 0, 0, 0, 0), yoq);
    }

    [Fact]
    public void Royxat_OperatorFiltri_HolatVaQBilanBirgaIshlaydi()
    {
        var hammasi = Toplam();
        var uniki = NasiyaXizmati.Royxat(hammasi, null, null, Bugun, 2);
        Assert.Equal(4, uniki.Count);
        Assert.All(uniki, n => Assert.Equal(2, n.OperatorId));
        Assert.Equal(6, NasiyaXizmati.Royxat(hammasi, null, null, Bugun).Count);                                  // operatorsiz - hammasi

        Assert.Equal(new[] { 1 }, NasiyaXizmati.Royxat(hammasi, "faol", null, Bugun, 2).Select(n => n.Id));              // qoldig'i bor
        Assert.Equal(new[] { 2, 3, 4 }, NasiyaXizmati.Royxat(hammasi, "yopilgan", null, Bugun, 2).Select(n => n.Id).Order());   // eski qarz ham yopilgan
        Assert.Equal(new[] { 1, 3 }, NasiyaXizmati.Royxat(hammasi, null, "bobur", Bugun, 2).Select(n => n.Id).Order());
        Assert.Equal(new[] { 1, 3, 5 }, NasiyaXizmati.Royxat(hammasi, null, "bobur", Bugun).Select(n => n.Id).Order());
        Assert.Empty(NasiyaXizmati.Royxat(hammasi, null, null, Bugun, 99));
    }

    [Fact]
    public void HisobotTuz_EskiQarzYigindisiVaSoniDtoga()
    {
        var h = HisobotXizmati.Tuz(HisobotGuruhi.Smena, new List<Smena>(), new Dictionary<int, string>(), [], 0, null, 420_000, 2);
        Assert.Equal((420_000L, 2), (h.EskiQarzQaytgan, h.EskiQarzSoni));
        var bosh = HisobotXizmati.Tuz(HisobotGuruhi.Smena, new List<Smena>(), new Dictionary<int, string>(), [], 0);
        Assert.Equal((0L, 0), (bosh.EskiQarzQaytgan, bosh.EskiQarzSoni));
    }
}
