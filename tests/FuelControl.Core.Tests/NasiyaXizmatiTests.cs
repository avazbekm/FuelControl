using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

public class NasiyaXizmatiTests
{
    private static readonly DateOnly Bugun = new(2026, 10, 4);
    private static readonly DateTime OyBoshi = new(2026, 9, 30, 19, 0, 0, DateTimeKind.Utc);       // 1 oktabr 00:00 Toshkent
    private static readonly DateTime KeyingiOy = new(2026, 10, 31, 19, 0, 0, DateTimeKind.Utc);    // 1 noyabr 00:00 Toshkent

    private static Nasiya Yangi(long summa = 600_000, DateOnly? muddat = null) =>
        NasiyaXizmati.Yarat(42, 2, "Alisher", "  Bobur Aliyev ", " +998 97 700 80 90 ", "01 H 202 MA", summa, muddat ?? new DateOnly(2026, 10, 11), " Neksiya ", Namuna.Vaqt());

    [Fact]
    public void Yarat_MalumotlarTozalanadi()
    {
        var n = Yangi();
        Assert.Equal(("Bobur Aliyev", "+998 97 700 80 90", "01 H 202 MA", "Neksiya"), (n.MijozIsmi, n.Telefon, n.MashinaRaqami, n.Izoh));
        Assert.Equal((42, 2, "Alisher", 600_000L, 0L, 600_000L), (n.SmenaId, n.OperatorId, n.KimYozdi, n.Summa, n.Qaytgan, n.Qoldiq));
        Assert.Null(n.Yopildi);
    }

    [Fact]
    public void Yarat_IsmYokiSummaNotogri_RadEtiladi()
    {
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", " ", "901234567", "1", 1000, Bugun, null, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", null, "901234567", "1", 1000, Bugun, null, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", "Ali", "901234567", "1", 0, Bugun, null, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", new string('a', 101), "901234567", "1", 1000, Bugun, null, Namuna.Vaqt()));
        var faqatTelefon = NasiyaXizmati.Yarat(1, 1, "Op", "Ali", "+998901234567", null, 1000, Bugun, "  ", Namuna.Vaqt());   // izoh ixtiyoriy
        Assert.Equal(("+998 90 123 45 67", "", null), (faqatTelefon.Telefon, faqatTelefon.MashinaRaqami, faqatTelefon.Izoh));      // telefon normallashtirilgan
        var faqatRaqam = NasiyaXizmati.Yarat(1, 1, "Op", "Ali", " ", "01 A 777 BC", 1000, Bugun, null, Namuna.Vaqt());
        Assert.Equal(("", "01 A 777 BC"), (faqatRaqam.Telefon, faqatRaqam.MashinaRaqami));
    }

    [Fact]
    public void Yarat_TelefonYokiMashinaRaqamidanKamidaBittasiMajburiy()
    {
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", "Ali", null, null, 1000, Bugun, null, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", "Ali", "  ", " ", 1000, Bugun, null, Namuna.Vaqt()));
    }

    [Fact]
    public void Yarat_MuddatYozilayotganKundanOldinBolmaydi_ToshkentSanasiBilan()
    {
        // 4-oktabr 12:00 UTC = 4-oktabr 17:00 Toshkent: kecha (3-oktabr) rad, bugun (4-oktabr) mumkin.
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", "Ali", "901234567", null, 1000, new DateOnly(2026, 10, 3), null, Namuna.Vaqt()));
        Assert.NotNull(NasiyaXizmati.Yarat(1, 1, "Op", "Ali", "901234567", null, 1000, new DateOnly(2026, 10, 4), null, Namuna.Vaqt()));

        // 4-oktabr 20:00 UTC = 5-oktabr 01:00 Toshkent: Toshkent bo'yicha bugun 5-oktabr, shuning uchun 4-oktabr muddati o'tgan.
        var kech = new DateTime(2026, 10, 4, 20, 0, 0, DateTimeKind.Utc);
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Yarat(1, 1, "Op", "Ali", "901234567", null, 1000, new DateOnly(2026, 10, 4), null, kech));
        Assert.NotNull(NasiyaXizmati.Yarat(1, 1, "Op", "Ali", "901234567", null, 1000, new DateOnly(2026, 10, 5), null, kech));
    }

    [Fact]
    public void Qaytish_QismanVaToliq_QoldiqVaYopilganVaqt()
    {
        var n = Yangi();
        var q1 = NasiyaXizmati.Qaytish(n, 300_000, TolovTuri.Naqd, 42, 2, "Alisher", " qisman ", Namuna.Vaqt(5));
        Assert.Equal((300_000L, 300_000L), (n.Qaytgan, n.Qoldiq));
        Assert.Null(n.Yopildi);
        Assert.Equal((300_000L, TolovTuri.Naqd, 42, "qisman"), (q1.Summa, q1.Usul, q1.SmenaId, q1.Izoh));

        NasiyaXizmati.Qaytish(n, 300_000, TolovTuri.Depozit, null, 2, "Boshliq", null, Namuna.Vaqt(7));
        Assert.Equal(0, n.Qoldiq);
        Assert.Equal(Namuna.Vaqt(7), n.Yopildi);
        Assert.Equal(NasiyaHolati.Yopilgan, NasiyaXizmati.Holat(n, Bugun));
    }

    [Theory]
    [InlineData(TolovTuri.Naqd)]
    [InlineData(TolovTuri.Plastik)]
    [InlineData(TolovTuri.Depozit)]
    public void Qaytish_UchUsulHam_Qabul(TolovTuri usul)
    {
        var n = Yangi();
        var q = NasiyaXizmati.Qaytish(n, 100_000, usul, 42, 2, "Op", null, Namuna.Vaqt());
        Assert.Equal(usul, q.Usul);
        Assert.Equal(500_000, n.Qoldiq);
    }

    [Fact]
    public void Qaytish_QoldiqdanKopYokiNolYokiNotogriUsul_RadEtiladi()
    {
        var n = Yangi();
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Qaytish(n, 600_001, TolovTuri.Naqd, 42, 2, "Op", null, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Qaytish(n, 0, TolovTuri.Naqd, 42, 2, "Op", null, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Qaytish(n, -5, TolovTuri.Naqd, 42, 2, "Op", null, Namuna.Vaqt()));
        Assert.Throws<ArgumentException>(() => NasiyaXizmati.Qaytish(n, 100, (TolovTuri)9, 42, 2, "Op", null, Namuna.Vaqt()));
        Assert.Equal(0, n.Qaytgan);
    }

    [Fact]
    public void QaytishniOlibTashlash_QoldiqTiklanadi_NasiyaQaytaOchiladi()
    {
        var n = Yangi();
        NasiyaXizmati.Qaytish(n, 200_000, TolovTuri.Naqd, 42, 2, "Op", null, Namuna.Vaqt());
        var q2 = NasiyaXizmati.Qaytish(n, 400_000, TolovTuri.Plastik, 42, 2, "Op", null, Namuna.Vaqt(6));
        Assert.NotNull(n.Yopildi);

        NasiyaXizmati.QaytishniOlibTashla(n, q2);

        Assert.Equal((200_000L, 400_000L), (n.Qaytgan, n.Qoldiq));
        Assert.Null(n.Yopildi);
        Assert.Throws<InvalidOperationException>(() => NasiyaXizmati.QaytishniOlibTashla(n, new NasiyaQaytishi { Summa = 900_000 }));
    }

    [Theory]
    [InlineData("2026-09-30", 600_000, 0, NasiyaHolati.MuddatiOtgan, -4)]
    [InlineData("2026-10-04", 600_000, 0, NasiyaHolati.Faol, 0)]             // muddat bugun - hali o'tmagan
    [InlineData("2026-10-11", 350_000, 0, NasiyaHolati.Faol, 7)]
    [InlineData("2026-09-30", 600_000, 600_000, NasiyaHolati.Yopilgan, -4)]  // to'liq qaytgan - muddat o'tgan bo'lsa ham yopilgan
    public void Holat_SanagaQarab(string muddat, long summa, long qaytgan, NasiyaHolati kutilgan, int kun)
    {
        var n = new Nasiya { Summa = summa, Qaytgan = qaytgan, Muddat = DateOnly.Parse(muddat) };
        Assert.Equal(kutilgan, NasiyaXizmati.Holat(n, Bugun));
        Assert.Equal(kun, NasiyaXizmati.MuddatgachaKun(n, Bugun));
    }

    [Fact]
    public void HolatFiltri_FaolQarziBorHammasi_OtganFaqatMuddatiOtgan_YopilganNol()
    {
        var otgan = new Nasiya { Summa = 100, Muddat = new DateOnly(2026, 9, 30) };
        var faol = new Nasiya { Summa = 100, Muddat = new DateOnly(2026, 10, 11) };
        var yopilgan = new Nasiya { Summa = 100, Qaytgan = 100, Muddat = new DateOnly(2026, 9, 1) };
        bool F(string? h, Nasiya n) => NasiyaXizmati.HolatFiltri(h, n, Bugun);

        Assert.Equal([true, true, false], new[] { F("faol", otgan), F("faol", faol), F("faol", yopilgan) });
        Assert.Equal([true, false, false], new[] { F("OTGAN", otgan), F("otgan", faol), F("otgan", yopilgan) });
        Assert.Equal([false, false, true], new[] { F("yopilgan", otgan), F("yopilgan", faol), F("yopilgan", yopilgan) });
        Assert.Equal([true, true, true], new[] { F(null, otgan), F("", faol), F(" ", yopilgan) });
        Assert.Throws<ArgumentException>(() => F("xato", faol));
    }

    private static DateTime U(int oy, int kun, int soat) => new(2026, oy, kun, soat, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Xulosa_FaolMuddatiOtganOyBerilganOyQaytgan()
    {
        var nasiyalar = new List<Nasiya>
        {
            new() { Summa = 300_000, Muddat = new DateOnly(2026, 9, 30), Yozildi = U(9, 23, 5) },                      // o'tgan, o'tgan oy
            new() { Summa = 180_000, Muddat = new DateOnly(2026, 9, 28), Yozildi = U(10, 1, 5) },                      // o'tgan, shu oy
            new() { Summa = 350_000, Muddat = new DateOnly(2026, 10, 11), Yozildi = U(10, 4, 5) },                     // faol
            new() { Summa = 220_000, Qaytgan = 20_000, Muddat = new DateOnly(2026, 10, 7), Yozildi = U(10, 4, 6) },
            new() { Summa = 90_000, Qaytgan = 90_000, Muddat = new DateOnly(2026, 10, 2), Yozildi = U(10, 1, 8) },     // yopilgan
        };
        var qaytishlar = new List<NasiyaQaytishi>
        {
            new() { Summa = 20_000, Vaqt = U(10, 4, 7) },
            new() { Summa = 90_000, Vaqt = U(10, 3, 7) },
            new() { Summa = 55_000, Vaqt = U(9, 25, 7) },                                                              // o'tgan oy
        };

        var x = NasiyaXizmati.Xulosa(nasiyalar, qaytishlar, Bugun, OyBoshi, KeyingiOy);

        Assert.Equal((300_000L + 180_000 + 350_000 + 200_000, 4), (x.FaolQarz, x.FaolSoni));
        Assert.Equal((480_000L, 2), (x.MuddatiOtgan, x.MuddatiOtganSoni));
        Assert.Equal((180_000L + 350_000 + 220_000 + 90_000, 4), (x.OyBerilgan, x.OyBerilganSoni));
        Assert.Equal((110_000L, 2), (x.OyQaytgan, x.OyQaytganSoni));
    }
}
