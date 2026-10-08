using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>GET /nasiyalar/mijozlar: mijozlar nasiya yozuvlaridan yig'iladi (docs 8-bo'lim, 2-band).</summary>
public class MijozTakliflariTests
{
    private static int _id;

    private static Nasiya N(string ism, string tel = "", string raqam = "", int kun = 1, int soat = 5, long summa = 100_000, long qaytgan = 0) =>
        new() { Id = Interlocked.Increment(ref _id), MijozIsmi = ism, Telefon = tel, MashinaRaqami = raqam, Summa = summa, Qaytgan = qaytgan,
                Muddat = new DateOnly(2026, 10, 20), Yozildi = new DateTime(2026, 10, kun, soat, 0, 0, DateTimeKind.Utc) };

    private static Nasiya[] Namuna() =>
    [
        N("Jasur To'xtayev", "+998 90 123 45 67", "01 A 777 BC", kun: 4, summa: 350_000),                     // Jasur: eng yangi nasiyasi
        N("Jasur Toxtayev", "+998 90 123 45 67", "01 B 111 AA", kun: 2, summa: 200_000, qaytgan: 50_000),      // shu telefon - o'sha mijoz
        N("Farhod Ismoilov", "+998 93 555 12 34", "30 B 456 CA", kun: 4, soat: 9, summa: 220_000),
        N("Ali Valiyev", "", "01 K 100 KA", kun: 3, summa: 100_000),                                         // telefon yo'q: ism + raqam bo'yicha
        N("ali  valiyev", "", "01k100ka", kun: 1, summa: 60_000, qaytgan: 60_000),                           // o'sha mijoz (katta-kichik harf, bo'shliq)
        N("Ali Valiyev", "", "01 K 999 KA", kun: 2, summa: 70_000),                                          // boshqa mashina - boshqa mijoz
    ];

    [Fact]
    public void Sorovsiz_EngOxirgiMijozlar_TelefonYokiIsmRaqamBoyichaGuruhlangan()
    {
        var t = NasiyaXizmati.MijozTakliflari(Namuna(), null);

        Assert.Equal(["Farhod Ismoilov", "Jasur To'xtayev", "Ali Valiyev", "Ali Valiyev"], t.Select(x => x.MijozIsmi));
        var jasur = t[1];
        // Ikki nasiya (ikki yozilishda ism va mashina farqli), FaolQarz = 350 000 + (200 000 - 50 000), eng yangi nasiya 4-oktabr 10:00 Toshkent.
        Assert.Equal(("+998 90 123 45 67", "01 A 777 BC", 2, 500_000L, new DateOnly(2026, 10, 4)), (jasur.Telefon, jasur.MashinaRaqami, jasur.NasiyaSoni, jasur.FaolQarz, jasur.OxirgiNasiya));
        var ali = t[2];
        Assert.Equal(("", "01 K 100 KA", 2, 100_000L, new DateOnly(2026, 10, 3)), (ali.Telefon, ali.MashinaRaqami, ali.NasiyaSoni, ali.FaolQarz, ali.OxirgiNasiya));
        Assert.Equal(("01 K 999 KA", 1, 70_000L), (t[3].MashinaRaqami, t[3].NasiyaSoni, t[3].FaolQarz));
    }

    [Fact]
    public void FaolQarz_YopilganNasiyaQoldigiNol_OxirgiNasiyaToshkentSanasi()
    {
        var yopiq = N("Yopiq Mijoz", "+998 91 000 00 01", kun: 3, summa: 90_000, qaytgan: 90_000);
        var kechqurun = N("Kech Mijoz", "+998 91 000 00 02", kun: 4, soat: 20);        // 4-oktabr 20:00 UTC = 5-oktabr 01:00 Toshkent

        var t = NasiyaXizmati.MijozTakliflari([yopiq, kechqurun], null);

        Assert.Equal([0L, 100_000L], t.OrderBy(x => x.MijozIsmi == "Yopiq Mijoz" ? 0 : 1).Select(x => x.FaolQarz));
        Assert.Equal(new DateOnly(2026, 10, 5), t.Single(x => x.MijozIsmi == "Kech Mijoz").OxirgiNasiya);
        Assert.Equal(1, t.Single(x => x.MijozIsmi == "Yopiq Mijoz").NasiyaSoni);          // yopilgan mijoz ham taklif qilinadi
    }

    [Fact]
    public void TaklifdagiIsmVaRaqam_SorovgaEngMosYozuvdan_SoniVaQarzHammaNasiyalardan()
    {
        // Eski mashina raqami bo'yicha qidirilsa - o'sha mashina taklif qilinadi.
        var eski = NasiyaXizmati.MijozTakliflari(Namuna(), "111aa").Single();
        Assert.Equal(("Jasur Toxtayev", "01 B 111 AA", 2, 500_000L), (eski.MijozIsmi, eski.MashinaRaqami, eski.NasiyaSoni, eski.FaolQarz));
        Assert.Equal(new DateOnly(2026, 10, 4), eski.OxirgiNasiya);                  // "oxirgi" - mijozning eng yangi nasiyasi, mos kelgani emas

        // Telefon bo'yicha ikkala yozuv teng mos - eng yangisi.
        var telefon = NasiyaXizmati.MijozTakliflari(Namuna(), "4567").Single();
        Assert.Equal(("Jasur To'xtayev", "01 A 777 BC"), (telefon.MijozIsmi, telefon.MashinaRaqami));

        // Apostrof tashlanadi: har qanday yozilishda ikkala yozuv (bitta mijoz) topiladi; teng mos - eng yangi yozuv.
        foreach (var q in new[] { "to" + (char)0x2018 + "xtayev", "TO`XTAYEV", "toxtayev", "To'xtayev" })
        {
            var t = NasiyaXizmati.MijozTakliflari(Namuna(), q).Single();
            Assert.Equal(("Jasur To'xtayev", 2), (t.MijozIsmi, t.NasiyaSoni));
        }
    }

    [Fact]
    public void Apostrof_TelefonsizMijozlar_ApostrofliVaApostrofsizIsmBirMijoz()
    {
        var yozuvlar = new[]
        {
            N("Ulug'bek Nazarov", "", "01 K 515 KA", kun: 1),
            N("Ulugbek Nazarov", "", "01k515ka", kun: 2),
            N("Ulug" + (char)0x02BB + "bek  Nazarov", "", "01 K 515 KA", kun: 3),
            N("Ulug'bek Nazarov", "", "01 K 777 KA", kun: 4),              // boshqa mashina - boshqa mijoz
        };

        var t = NasiyaXizmati.MijozTakliflari(yozuvlar, null);

        Assert.Equal(2, t.Length);
        Assert.Equal(("01 K 777 KA", 1), (t[0].MashinaRaqami, t[0].NasiyaSoni));
        Assert.Equal(("01 K 515 KA", 3), (t[1].MashinaRaqami, t[1].NasiyaSoni));
    }

    [Fact]
    public void Apostrof_QidiruvdaUlugbekVaUlugbek_BirbirniTopadi()
    {
        var yozuvlar = new[]
        {
            N("Ulug'bek Nazarov", "+998 91 333 44 55", "01 K 515 KA", kun: 1),
            N("Ulugbek Karimov", "+998 90 000 00 02", "01 U 002 UU", kun: 2),
            N("Jasur Toxtayev", "+998 90 000 00 03", "01 A 003 AA", kun: 3),
        };

        foreach (var q in new[] { "ulugbek", "Ulug'bek", "ULUG" + (char)0x2019 + "BEK", "ulug" + (char)0x02BC + "bek" })
            Assert.Equal(["Ulugbek Karimov", "Ulug'bek Nazarov"], NasiyaXizmati.MijozTakliflari(yozuvlar, q).Select(x => x.MijozIsmi));
        foreach (var q in new[] { "To'xtayev", "TO`XTAYEV", "toxtayev" })
            Assert.Equal(["Jasur Toxtayev"], NasiyaXizmati.MijozTakliflari(yozuvlar, q).Select(x => x.MijozIsmi));
        Assert.Equal(["Ulug'bek Nazarov"], NasiyaXizmati.MijozTakliflari(yozuvlar, "Ulugbek Nazarov").Select(x => x.MijozIsmi));   // aynan mos
    }

    [Fact]
    public void Tartib_AynanVaBoshidanMosOldin_KeyinQolganlari_YangisiOldinda()
    {
        var yozuvlar = new[]
        {
            N("Vali Karimov", "+998 90 000 00 01", kun: 9),                // "ali" - ichidan
            N("Alisher Qosimov", "+998 90 000 00 02", kun: 3),             // boshidan
            N("Ali", "+998 90 000 00 03", kun: 1),                         // aynan
            N("Sardor Ali", "+998 90 000 00 04", kun: 5),                  // so'z boshidan
            N("Alijon Rahmonov", "+998 90 000 00 05", kun: 7),             // boshidan, yangiroq
        };

        var t = NasiyaXizmati.MijozTakliflari(yozuvlar, "ali");

        Assert.Equal(["Ali", "Alijon Rahmonov", "Sardor Ali", "Alisher Qosimov", "Vali Karimov"], t.Select(x => x.MijozIsmi));
    }

    [Fact]
    public void Chegara_EngKopi8Ta_YangisiOldinda()
    {
        var yozuvlar = Enumerable.Range(1, 12).Select(i => N("Mijoz " + i, "+998 90 000 00 " + i.ToString("00"), kun: i)).ToArray();

        var t = NasiyaXizmati.MijozTakliflari(yozuvlar, null);

        Assert.Equal(8, t.Length);
        Assert.Equal(Enumerable.Range(5, 8).Reverse().Select(i => "Mijoz " + i), t.Select(x => x.MijozIsmi));
        Assert.Equal(3, NasiyaXizmati.MijozTakliflari(yozuvlar, "mijoz 1", 3).Length);        // "mijoz 1", "mijoz 10", 11, 12 mos; maks = 3
        Assert.Equal(4, NasiyaXizmati.MijozTakliflari(yozuvlar, "mijoz 1").Length);
    }

    [Fact]
    public void EskiNormallashtirilmaganTelefonlar_BirMijozgaBirlashadi_TelefonsizBilanAralashmaydi()
    {
        var yozuvlar = new[]
        {
            N("Bobur", "+998901234567", "01 H 202 MA", kun: 1),
            N("Bobur A.", "90-123-45-67", "01 H 202 MA", kun: 2),
            N("Bobur", "", "01 H 202 MA", kun: 3),                  // telefonsiz: ism + raqam bo'yicha alohida mijoz
        };

        var t = NasiyaXizmati.MijozTakliflari(yozuvlar, null);

        Assert.Equal(2, t.Length);
        Assert.Equal((1, 100_000L), (t[0].NasiyaSoni, t[0].FaolQarz));            // telefonsiz (eng yangi)
        Assert.Equal((2, 200_000L), (t[1].NasiyaSoni, t[1].FaolQarz));
    }

    [Fact]
    public void MosKelmasa_BoshRoyxat_NasiyaYoq_BoshRoyxat()
    {
        Assert.Empty(NasiyaXizmati.MijozTakliflari(Namuna(), "topilmaydi"));
        Assert.Empty(NasiyaXizmati.MijozTakliflari([], null));
        Assert.Empty(NasiyaXizmati.MijozTakliflari([], "ali"));
    }
}
