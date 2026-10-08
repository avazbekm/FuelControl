using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Xunit;

namespace FuelControl.Core.Tests;

/// <summary>Nasiya qidiruvi: /nasiyalar?q= va /nasiyalar/mijozlar?q= uchun yagona qoida (docs 8-bo'lim, 3-band).</summary>
public class NasiyaQidiruvTests
{
    private static readonly char[] Apostroflar = [(char)39, (char)0x2018, (char)0x2019, (char)96, (char)0x02BB, (char)0x02BC, (char)0xB4];
    private static int _id;

    private static Nasiya N(string ism, string tel = "", string raqam = "", int kun = 1, int soat = 5, long summa = 100_000, long qaytgan = 0) =>
        new() { Id = Interlocked.Increment(ref _id), MijozIsmi = ism, Telefon = tel, MashinaRaqami = raqam, Summa = summa, Qaytgan = qaytgan,
                Muddat = new DateOnly(2026, 10, 20), Yozildi = new DateTime(2026, 10, kun, soat, 0, 0, DateTimeKind.Utc) };

    private static bool Mos(Nasiya n, string? q) => NasiyaXizmati.QidiruvgaMos(n, q);

    private static int? Daraja(Nasiya n, string? q) => NasiyaQidiruvi.Tayyorla(q).Daraja(n);

    [Fact]
    public void Ism_KattaKichikHarfSozBoshiVaIchidagiQism()
    {
        var n = N("Bobur Aliyev");
        foreach (var q in new[] { "bobur", "BOBUR", "Aliyev", "aliyev", "liyev", "obur ali", "  bobur   aliyev ", "r a" })
            Assert.True(Mos(n, q), q);
        foreach (var q in new[] { "sherzod", "aliyevv", "aliyev bobur" })
            Assert.False(Mos(n, q), q);
    }

    [Fact]
    public void Ism_KirillchaKattaKichikHarf()
    {
        Assert.True(Mos(N("Тошматов Жасур"), "ТОШМАТ"));
        Assert.True(Mos(N("Тошматов Жасур"), "жасур"));
    }

    [Fact]
    public void Ism_ApostrofTurlariBirXil_HammasiBirbiriBilanMos()
    {
        foreach (var saqlangan in Apostroflar)
            foreach (var yozilgan in Apostroflar)
                Assert.True(Mos(N("To" + saqlangan + "xtayev Jasur"), "to" + yozilgan + "xtayev"), $"{(int)saqlangan:X} / {(int)yozilgan:X}");
        Assert.True(Mos(N("To'xtayev"), "To" + (char)0x2018 + "xtayev"));
        Assert.True(Mos(N("To" + (char)0x2018 + "xtayev"), "To'xtayev"));
    }

    [Fact]
    public void Ism_ApostrofTashlanadi_ApostrofliVaApostrofsizBirbirniTopadi()
    {
        // "O'xshash ismlar chiqsin": odamlar apostrofni ko'pincha yozmaydi, shuning uchun so'rovda ham, ismda ham tashlanadi.
        foreach (var (ism, q) in new[]
        {
            ("To'xtayev", "Toxtayev"), ("Toxtayev", "To'xtayev"), ("Toxtayev", "TO`XTAYEV"), ("To'xtayev", "TO`XTAYEV"),
            ("Ulug'bek Nazarov", "Ulugbek"), ("Ulugbek Karimov", "Ulug'bek"), ("Ulug'bek", "ulugbek"), ("Ulugbek", "ULUG'BEK"),
            ("G" + (char)0x2018 + "ayrat", "gayrat"), ("O" + (char)0x02BB + "ktam", "oktam"), ("Oktam", "o" + (char)0x02BC + "ktam"),
            ("Sa'id Karimov", "SAID"), ("Sa'id Karimov", "said karimov"), ("Sa" + (char)0xB4 + "id", "Sa'id"),
        })
            Assert.True(Mos(N(ism), q), $"{ism} <- {q}");
        foreach (var apostrof in Apostroflar)                                          // har bir turi bilan, ikkala tomonga
        {
            Assert.True(Mos(N("Ulug" + apostrof + "bek"), "ulugbek"));
            Assert.True(Mos(N("Ulugbek"), "ulug" + apostrof + "bek"));
        }
        Assert.False(Mos(N("Toxtayev"), "To'xtayeva"));                                // apostrof tashlanadi, qolgani solishtiriladi
        Assert.False(Mos(N("Ulug'bek"), "ulubek"));
    }

    [Fact]
    public void Ism_FaqatApostrofdanIboratSorov_BoshSorovHisoblanadi()
    {
        var n = N("Jasur");
        Assert.True(Mos(n, "'"));
        Assert.True(Mos(n, " " + (char)0x2018 + (char)0x2019 + "` "));
        Assert.True(NasiyaQidiruvi.Tayyorla("'").Bosh);
        Assert.False(Mos(n, "+"));                                                      // apostrof emas - oddiy belgi, hech narsaga mos emas
    }

    [Fact]
    public void Telefon_FaqatRaqamlar_MamlakatKodiVaAjratgichlarEtiborsiz_KamidaUchtaRaqam()
    {
        var n = N("Jasur", "+998 90 123 45 67");
        foreach (var q in new[] { "4567", "45 67", "45-67", "123", "+998 90 123", "+998-90-123-45-67", "901234567", "998901234567", "90 123 45 67" })
            Assert.True(Mos(n, q), q);
        foreach (var q in new[] { "12", "45", "+998", "+998 9", "+998 90", "999", "4568", "+998 91" })
            Assert.False(Mos(n, q), q);                    // 3 raqamdan kam yoki mos emas; "+998" yolg'iz o'zi - hammaga mos kelmaydi
    }

    [Fact]
    public void Telefon_EskiNormallashtirilmaganSaqlanganQiymatHamTopiladi()
    {
        var n = N("Jasur", "+998901234567");
        Assert.True(Mos(n, "4567"));
        Assert.True(Mos(n, "+998 90 123"));
    }

    [Fact]
    public void MashinaRaqami_BoshliqsizVaKattaHarfda()
    {
        var n = N("Jasur", "", "01 A 777 BC");
        foreach (var q in new[] { "777bc", "777 BC", "01a777bc", "a 777", "01 A 777 BC", "7bc" })
            Assert.True(Mos(n, q), q);
        foreach (var q in new[] { "777bd", "02a" })
            Assert.False(Mos(n, q), q);
    }

    [Fact]
    public void BoshSorov_HammaMos_BelgilarFaqatBolsa_HechNarsaMosEmas()
    {
        var n = N("Jasur", "+998 90 123 45 67", "01 A 777 BC");
        Assert.True(Mos(n, null));
        Assert.True(Mos(n, ""));
        Assert.True(Mos(n, "   "));
        Assert.False(Mos(n, "---"));
        Assert.False(Mos(n, "+"));
    }

    [Fact]
    public void Daraja_AynanBoshidanIchidan()
    {
        Assert.Equal(0, Daraja(N("Bobur Aliyev"), "bobur aliyev"));
        Assert.Equal(1, Daraja(N("Bobur Aliyev"), "bob"));                     // ism boshidan
        Assert.Equal(1, Daraja(N("Bobur Aliyev"), "ali"));                     // ismdagi so'z boshidan
        Assert.Equal(2, Daraja(N("Bobur Aliyev"), "liyev"));                   // ichidan
        Assert.Equal(0, Daraja(N("To'xtayev"), "toxtayev"));                   // apostrofsiz yozilgan - aynan mos
        Assert.Equal(1, Daraja(N("Ulug'bek Nazarov"), "ulugbek"));             // boshidan
        Assert.Equal(1, Daraja(N("Jasur To'xtayev"), "toxt"));                 // so'z boshidan (apostrof tashlangan)
        Assert.Equal(0, Daraja(N("X", "+998 90 123 45 67"), "901234567"));
        Assert.Equal(1, Daraja(N("X", "+998 90 123 45 67"), "+998 90 123"));
        Assert.Equal(2, Daraja(N("X", "+998 90 123 45 67"), "4567"));
        Assert.Equal(0, Daraja(N("X", "", "01 A 777 BC"), "01a777bc"));
        Assert.Equal(1, Daraja(N("X", "", "01 A 777 BC"), "01a7"));
        Assert.Equal(2, Daraja(N("X", "", "01 A 777 BC"), "777bc"));
        Assert.Null(Daraja(N("Bobur"), "sherzod"));
        Assert.Equal(0, Daraja(N("Bobur"), null));
        // Eng yaxshi daraja: ism ichidan (2), lekin mashina raqami aynan mos (0).
        Assert.Equal(0, Daraja(N("Ali 777", "", "777"), "777"));
    }

    [Fact]
    public void Royxat_SorovBilan_AynanBoshidanOldin_KeyinQolganlari_YangisiOldinda()
    {
        var ali = N("Ali", kun: 1);                       // aynan
        var alisher = N("Alisher", kun: 3);               // boshidan
        var sardorAli = N("Sardor Ali", kun: 2);          // so'z boshidan
        var vali = N("Vali", kun: 4);                     // ichidan, eng yangisi
        var boshqa = N("Bobur", kun: 5);
        var bugun = new DateOnly(2026, 10, 6);

        var natija = NasiyaXizmati.Royxat([vali, boshqa, sardorAli, ali, alisher], null, "ali", bugun);

        Assert.Equal([ali.Id, alisher.Id, sardorAli.Id, vali.Id], natija.Select(x => x.Id));
    }

    [Fact]
    public void Royxat_SorovsizEskiTartib_QarziBorlarMuddatBoyicha_KeyinYopilganlar()
    {
        var kech = N("Kech"); kech.Muddat = new DateOnly(2026, 10, 30);
        var erta = N("Erta"); erta.Muddat = new DateOnly(2026, 10, 10);
        var yopiq = N("Yopiq", qaytgan: 100_000); yopiq.Muddat = new DateOnly(2026, 9, 1);

        var natija = NasiyaXizmati.Royxat([yopiq, kech, erta], null, null, new DateOnly(2026, 10, 6));

        Assert.Equal([erta.Id, kech.Id, yopiq.Id], natija.Select(x => x.Id));
        Assert.Equal([erta.Id], NasiyaXizmati.Royxat([yopiq, kech, erta], "faol", "ert", new DateOnly(2026, 10, 6)).Select(x => x.Id));
        Assert.Empty(NasiyaXizmati.Royxat([yopiq, kech, erta], "yopilgan", "ert", new DateOnly(2026, 10, 6)));
    }
}
