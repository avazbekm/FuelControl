using FuelControl.Api.Data;
using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;
using Microsoft.EntityFrameworkCore;

namespace FuelControl.Api.Xizmatlar;

/// <summary>
/// Faqat Development/Web + "Seed:DemoMalumot=true" va BO'SH bazada (hali smena yo'q): dizayndagi namuna (docs/dizayn). Smena #28-#41 yopilgan
/// (hammasida aparat segmentlari, har segment Boshi = oldingi smena Oxiri), #42 ochiq (nasiya, qaytish va xarajatlari bilan), 5 aparat (pult,
/// bak), bak kirimlari (tarix izchil, bak hech qachon manfiy emas), 8 nasiya, operator hisoblari va audit.
/// Raqamlar dizayn bilan bir xil: oy savdosi 48 590 420, kamomat 120 000 / ortiqcha 20 000, nasiya 2 510 000 (7 ta), muddati o'tgan 480 000 (2 ta).
/// </summary>
public static class DemoMalumot
{
    /// <summary>Toshkent mahalliy vaqti -> UTC (2026-yil).</summary>
    private static DateTime T(int oy, int kun, int soat, int minut) =>
        new DateTime(2026, oy, kun, soat, minut, 0, DateTimeKind.Utc).AddHours(-5);

    /// <summary>#41 yopilgandan keyingi holat (dizayndagi #42 ochilishi), 1..5-aparat: pult ko'rsatkichi va bak qoldig'i.</summary>
    private static readonly decimal[] HozirgiPult = [184_230.50m, 97_410.00m, 63_118.20m, 41_902.75m, 120_560.00m];
    private static readonly decimal[] HozirgiBak = [6_840m, 3_215m, 4_120m, 1_960m, 9_480m];

    /// <summary>#39-#41: har aparat litri (Hisobotlar.dc.html va Smenalar.dc.html). Jami 1205.60 / 1236.10 / 1198.40; savdo 16 098 210 / 16 496 940 / 15 995 270.</summary>
    private static readonly Dictionary<int, decimal[]> Litrlar = new()
    {
        [39] = [380.40m, 235.00m, 170.10m, 92.00m, 328.10m],
        [40] = [405.20m, 235.00m, 180.40m, 92.00m, 323.50m],
        [41] = [392.10m, 230.00m, 171.30m, 95.00m, 310.00m],
    };

    /// <summary>Boshqaruvdagi "oxirgi 14 smena savdosi" grafigi (mln so'm): #28 (20.09) ... #38 (30.09); #39-#41 aniq. Litr - aparatlar ulushi bo'yicha.</summary>
    private static readonly double[] Grafik = [15.2, 16.8, 14.9, 15.6, 17.1, 16.2, 15.4, 16.9, 15.8, 16.4, 15.1];
    private static readonly double[] Ulush = [0.30, 0.20, 0.14, 0.14, 0.22];      // 1..5-aparat (o'rtacha narx 13 476 so'm/litr)

    private static decimal[] GrafikLitrlari(double mln) =>
        Ulush.Select(u => Math.Round((decimal)(mln * 1_000_000 / 13_476 * u), 2)).ToArray();

    /// <summary>Smena operatori: dizayn bo'yicha #29 Alisher, #31 Dilshod, #39 Dilshod, #40 Alisher, #41 Dilshod, #42 Alisher.</summary>
    private static string Operator(int id) => id switch
    {
        28 or 30 or 31 or 33 or 35 or 37 or 39 or 41 => "dilshod",
        _ => "alisher",
    };

    private sealed record Pul(long Plastik, long DepozitFarqi, long OchishTerminal, long OchishDepozit, long Sanalgan, string? Izoh);

    /// <summary>
    /// #39-#41 pul qatorlari (Hisobotlar.dc.html): Kutilgan = Qaytim + Savdo + Qaytgan - Plastik - DepozitFarqi - Nasiya - Xarajat;
    /// Sanalgan = Kutilgan + farq (#39 +20 000 ortiqcha, #40 -120 000 kamomat, #41 farq yo'q: 8 350 270).
    /// </summary>
    private static readonly Dictionary<int, Pul> Pullar = new()
    {
        [39] = new(7_290_000, 3_365_900, 90_000, 150_000, 4_637_310, null),
        [40] = new(7_410_000, 2_100_000, 110_000, 600_000, 4_596_940, "Kassada 120 000 kam chiqdi"),
        [41] = new(7_330_000, 270_000, 150_000, 980_000, 8_350_270, null),
    };

    private static DateTime Boshlanishi(int id) => id switch
    {
        39 => T(10, 1, 8, 3),
        40 => T(10, 2, 8, 1),
        41 => T(10, 3, 8, 0),
        42 => T(10, 4, 8, 2),
        _ => T(9, 20 + (id - 28), 8, 0),
    };

    private static async Task<Dictionary<string, Foydalanuvchi>> Operatorlar(FuelControlDbContext db)
    {
        foreach (var (ism, login) in new[] { ("Alisher Karimov", "alisher"), ("Dilshod Rahimov", "dilshod") })
        {
            if (await db.Foydalanuvchilar.AnyAsync(f => f.Login == login)) continue;
            db.Foydalanuvchilar.Add(new Foydalanuvchi
            {
                ToliqIsm = ism, Login = login, Rol = Rol.Operator, OylikMaosh = 4_500_000,
                ParolXeshi = ParolXeshlash.Xeshla("1234"), Ruxsatlar = RuxsatXizmati.Standart(Rol.Operator).ToList(),
            });
        }
        await db.SaveChangesAsync();
        return await db.Foydalanuvchilar.Where(f => f.Login == "alisher" || f.Login == "dilshod").ToDictionaryAsync(f => f.Login);
    }

    private static SmenaKorsatkichi Segment(int smenaId, Aparat a, decimal boshi, decimal oxiri, long narx, DateTime vaqt)
    {
        var litr = SmenaHisoblagich.Litr(boshi, oxiri);
        return new SmenaKorsatkichi
        {
            SmenaId = smenaId, AparatId = a.Id, Tartib = 1, Boshi = boshi, Oxiri = oxiri, Narx = narx,
            Litr = litr, Summa = SmenaHisoblagich.Summa(litr, narx), Vaqt = vaqt,
        };
    }

    public static async Task Yoz(FuelControlDbContext db, ILogger log)
    {
        if (await db.Smenalar.AnyAsync())
        {
            log.LogInformation("Demo ma'lumot: bazada smena bor, yozilmadi (yangi demo uchun bazani o'chiring).");
            return;
        }
        var op = await Operatorlar(db);
        var aparatlar = await db.Aparatlar.OrderBy(a => a.Raqam).ToListAsync();
        if (aparatlar.Count != 5)
        {
            log.LogWarning("Demo ma'lumot uchun aynan 5 ta aparat kerak (hozir {Soni}): yozilmadi.", aparatlar.Count);
            return;
        }
        var narx = await db.Yoqilgilar.ToDictionaryAsync(y => y.Id, y => y.Narx);
        var yoqilgiNomi = await db.Yoqilgilar.ToDictionaryAsync(y => y.Id, y => y.Nomi);
        var narxi = aparatlar.Select(a => narx[a.YoqilgiTuriId]).ToArray();
        var audit = new List<AuditYozuvi>();
        void A(DateTime vaqt, string kim, string amal, string tafsilot, string tur) =>
            audit.Add(new AuditYozuvi { Vaqt = vaqt, Kim = kim, Amal = amal, Tafsilot = tafsilot, Tur = tur });
        const string Boshliq = "Administrator";

        // Aparatlar: #41 yopilgandan keyingi holat. Bak faqat smena yopilganda kamayadi, kirimda ko'payadi.
        for (var i = 0; i < 5; i++)
        {
            aparatlar[i].TotalLitr = HozirgiPult[i];
            aparatlar[i].BakQoldiq = HozirgiBak[i];
        }

        // Litrlar va pult ko'rsatkichlari zanjiri (#41 dan orqaga): har smenaning Boshi = oldingi smenaning Oxiri.
        var litr = new Dictionary<int, decimal[]>(Litrlar);
        for (var i = 0; i < Grafik.Length; i++) litr[28 + i] = GrafikLitrlari(Grafik[i]);
        var boshi = new Dictionary<int, decimal[]>();
        var oxiri = new Dictionary<int, decimal[]>();
        var keyingiBoshi = HozirgiPult.ToArray();
        for (var id = 41; id >= 28; id--)
        {
            oxiri[id] = keyingiBoshi;
            boshi[id] = keyingiBoshi.Select((o, i) => o - litr[id][i]).ToArray();
            keyingiBoshi = boshi[id];
        }

        // Nasiya (Nasiyalar.dc.html), qaytish va xarajatlar. Id'lar aniq: qaytishlar nasiyaga Id orqali bog'lanadi.
        var nasiyalar = new List<Nasiya>();
        var qaytishlar = new List<NasiyaQaytishi>();
        var xarajatlar = new List<Xarajat>();
        Nasiya N(int id, int smena, string oper, string mijoz, string tel, string raqam, long summa, DateOnly muddat, DateTime yozildi, string? izoh = null)
        {
            var o = op[oper];
            var n = NasiyaXizmati.Yarat(smena, o.Id, o.ToliqIsm, mijoz, tel, raqam, summa, muddat, izoh, yozildi);
            n.Id = id;
            nasiyalar.Add(n);
            A(yozildi, o.ToliqIsm, "Nasiya yozildi", AuditMatnlari.NasiyaYozildi(n), AuditTurlari.Nasiya);
            return n;
        }
        void Q(Nasiya n, int smena, string oper, long summa, DateTime vaqt)
        {
            var o = op[oper];
            var q = NasiyaXizmati.Qaytish(n, summa, TolovTuri.Naqd, smena, o.Id, o.ToliqIsm, null, vaqt);
            qaytishlar.Add(q);
            A(vaqt, o.ToliqIsm, "Qarz qaytdi", AuditMatnlari.QarzQaytdi(n, q), AuditTurlari.Nasiya);
        }
        void X(int smena, string oper, long summa, string sabab, XarajatManbai manba, DateTime vaqt)
        {
            var o = op[oper];
            var x = XarajatXizmati.Yarat(smena, o.Id, o.ToliqIsm, summa, sabab, manba, vaqt);
            xarajatlar.Add(x);
            A(vaqt, o.ToliqIsm, "Xarajat yozildi", AuditMatnlari.Xarajat(x), AuditTurlari.Xarajat);
        }
        N(1, 29, "alisher", "Sherzod Qodirov", "+998 99 410 22 11", "40 C 919 DA", 180_000, new DateOnly(2026, 9, 28), T(9, 21, 12, 30));
        var bobur = N(2, 31, "dilshod", "Bobur Aliyev", "+998 97 700 80 90", "01 H 202 MA", 600_000, new DateOnly(2026, 9, 30), T(9, 23, 10, 10), "Neksiya, oylikdan keyin");
        N(3, 39, "dilshod", "Nodir Xasanov", "+998 88 640 07 07", "01 D 128 EA", 165_000, new DateOnly(2026, 10, 15), T(10, 1, 11, 15));
        var ulugbek = N(4, 39, "dilshod", "Ulug'bek Nazarov", "+998 91 333 44 55", "01 K 515 KA", 450_000, new DateOnly(2026, 10, 8), T(10, 1, 14, 40));
        N(5, 40, "alisher", "Rustam Ergashev", "+998 90 909 30 30", "01 345 KBA", 1_200_000, new DateOnly(2026, 11, 2), T(10, 2, 10, 20));
        N(6, 41, "dilshod", "Komil Saidov", "+998 94 222 31 31", "01 M 345 OA", 95_000, new DateOnly(2026, 10, 5), T(10, 3, 11, 5));
        N(7, 42, "alisher", "Jasur To'xtayev", "+998 90 123 45 67", "01 A 777 BC", 350_000, new DateOnly(2026, 10, 11), T(10, 4, 9, 5));
        N(8, 42, "alisher", "Farhod Ismoilov", "+998 93 555 12 34", "30 B 456 CA", 220_000, new DateOnly(2026, 10, 7), T(10, 4, 9, 47));
        Q(ulugbek, 40, "alisher", 450_000, T(10, 2, 15, 10));          // "Yopilgan 02.10"
        Q(bobur, 42, "alisher", 300_000, T(10, 4, 11, 20));
        X(39, "dilshod", 210_000, "Sovutgich ta'miri", XarajatManbai.Kassa, T(10, 1, 12, 10));
        X(39, "dilshod", 100_000, "Tozalash vositalari", XarajatManbai.Depozit, T(10, 1, 16, 40));
        X(40, "alisher", 120_000, "Oyna yuvish vositasi", XarajatManbai.Kassa, T(10, 2, 11, 5));
        X(40, "alisher", 1_500_000, "Boshliq naqd oldi", XarajatManbai.Kassa, T(10, 2, 17, 20));
        X(41, "dilshod", 50_000, "Chiqindi olib ketish", XarajatManbai.Kassa, T(10, 3, 10, 30));
        X(42, "alisher", 85_000, "Lampochka va tozalash vositasi", XarajatManbai.Kassa, T(10, 4, 10, 15));
        X(42, "alisher", 1_500_000, "Boshliq naqd oldi", XarajatManbai.Kassa, T(10, 4, 12, 40));

        // Yopilgan smenalar #28-#41: hammasida aparat segmentlari (Boshi = oldingi smena Oxiri); #39-#41 pul qatorlari dizayndagidek.
        var smenalar = new List<Smena>();
        var segmentlar = new List<SmenaKorsatkichi>();
        for (var id = 28; id <= 41; id++)
        {
            var tugadi = Boshlanishi(id + 1);
            var s = new Smena { Id = id, OperatorId = op[Operator(id)].Id, Boshlandi = Boshlanishi(id), Tugadi = tugadi };
            var seg = aparatlar.Select((a, i) => Segment(id, a, boshi[id][i], oxiri[id][i], narxi[i], tugadi)).ToList();
            var savdo = seg.Sum(x => x.Summa);
            var p = Pullar.GetValueOrDefault(id);
            var plastik = p?.Plastik ?? savdo * 45 / 100 / 1000 * 1000;
            var depozit = p?.DepozitFarqi ?? savdo * 12 / 100 / 1000 * 1000;
            s.OchishQaytim = 100_000;
            s.OchishTerminal = p?.OchishTerminal ?? 100_000;
            s.OchishDepozit = p?.OchishDepozit ?? 500_000;
            s.YopishTerminal = s.OchishTerminal + plastik;
            s.YopishDepozit = s.OchishDepozit + depozit;
            s.Izoh = p?.Izoh;
            var natija = SmenaHisoblagich.Hisobla(s, s.YopishTerminal.Value, s.YopishDepozit.Value, p?.Sanalgan ?? 0, seg,
                SmenaHisoblagich.SmenaYigindilari(id, nasiyalar, qaytishlar, xarajatlar));
            if (p is null) natija = natija with { Farq = 0 };                  // oldingi smenalar: sanalgan = kutilgan, farq yo'q
            s.SanalganNaqd = p?.Sanalgan ?? natija.Kutilgan;
            SmenaHisoblagich.Qollash(s, natija);
            smenalar.Add(s);
            segmentlar.AddRange(seg);
            if (id < 39) continue;
            var ism = op[Operator(id)].ToliqIsm;
            A(s.Boshlandi, ism, "Smena ochildi", AuditMatnlari.SmenaOchildi(s), AuditTurlari.Smena);
            A(tugadi, ism, "Smena yopildi", AuditMatnlari.SmenaYopildi(s), AuditTurlari.Smena);
        }
        var joriy = new Smena
        {
            Id = 42, OperatorId = op["alisher"].Id, Boshlandi = Boshlanishi(42), OchishQaytim = 100_000, OchishTerminal = 200_000, OchishDepozit = 1_250_000,
        };
        smenalar.Add(joriy);
        A(joriy.Boshlandi, op["alisher"].ToliqIsm, "Smena ochildi", AuditMatnlari.SmenaOchildi(joriy), AuditTurlari.Smena);

        // Bak kirimlari (dizayndagi "oxirgi kirim" sanalari). Qoldiq oldin/keyin yozilish vaqtidagi bak: hozirgi bak + undan keyin yopilgan smenalarda
        // sotilgan - undan keyin yozilgan kirimlar. 5-aparat: 03.10 kelgan, #41 yopilgach (04.10) yozilgan - "bak 9 480 L bo'ldi".
        var kirimRoyxati = new[]
        {
            (Aparat: 3, Litr: 3_000m, Vaqt: T(9, 25, 10, 20), Yozildi: T(9, 25, 10, 20), Hujjat: "yuk xati 1169"),
            (Aparat: 1, Litr: 4_000m, Vaqt: T(9, 28, 15, 5), Yozildi: T(9, 28, 15, 5), Hujjat: "yuk xati 1172"),
            (Aparat: 2, Litr: 3_000m, Vaqt: T(10, 1, 14, 20), Yozildi: T(10, 1, 14, 20), Hujjat: "yuk xati 1174"),
            (Aparat: 0, Litr: 5_000m, Vaqt: T(10, 2, 11, 40), Yozildi: T(10, 2, 11, 40), Hujjat: "yuk xati 1175"),
            (Aparat: 4, Litr: 8_000m, Vaqt: T(10, 3, 16, 30), Yozildi: T(10, 4, 8, 10), Hujjat: "yuk xati 1176"),
        };
        var kirimlar = new List<BakKirim>();
        for (var i = 0; i < 5; i++)
        {
            var uniki = kirimRoyxati.Where(k => k.Aparat == i).ToList();
            var hodisalar = Enumerable.Range(28, 14).Select(id => (Vaqt: Boshlanishi(id + 1), Delta: -litr[id][i], Kirim: -1))
                .Concat(uniki.Select((k, j) => (Vaqt: k.Yozildi, Delta: k.Litr, Kirim: j))).OrderBy(h => h.Vaqt).ToList();
            var daraja = HozirgiBak[i] - hodisalar.Sum(h => h.Delta);            // birinchi hodisadan oldingi bak
            if (daraja < 0) throw new InvalidOperationException($"Demo ma'lumot: {i + 1}-aparat boshlang'ich baki manfiy ({daraja}).");
            foreach (var h in hodisalar)
            {
                daraja += h.Delta;
                if (daraja < 0) throw new InvalidOperationException($"Demo ma'lumot: {i + 1}-aparat baki manfiyga tushdi ({daraja}).");
                if (h.Kirim < 0) continue;
                var k = uniki[h.Kirim];
                var kirim = new BakKirim { AparatId = aparatlar[i].Id, Litr = k.Litr, QoldiqOldin = daraja - k.Litr, QoldiqKeyin = daraja, Vaqt = k.Vaqt, Hujjat = k.Hujjat, KimYozdi = Boshliq };
                kirimlar.Add(kirim);
                A(k.Vaqt, Boshliq, "Bakka kirim", AuditMatnlari.BakKirimi(aparatlar[i].Raqam, yoqilgiNomi[aparatlar[i].YoqilgiTuriId], kirim), AuditTurlari.Bak);
            }
        }

        // Operator hisoblari: Alisher 4 500 000 - 1 000 000 - 120 000 = 3 380 000; Dilshod 4 500 000 + 20 000 = 4 520 000.
        var harakatlar = op.Values.Select(o => new HisobHarakati
        {
            OperatorId = o.Id, Sana = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), Turi = HarakatTuri.Maosh,
            Summa = o.OylikMaosh > 0 ? o.OylikMaosh : 4_500_000, Izoh = "Oktabr oyi maoshi", KimYozdi = "Tizim", MaoshOyi = "2026-10",
        }).ToList();
        harakatlar.Add(new HisobHarakati { OperatorId = op["alisher"].Id, Sana = T(10, 2, 14, 10), Turi = HarakatTuri.Avans, Summa = -1_000_000, Izoh = "Naqd berildi", KimYozdi = Boshliq });
        harakatlar.Add(new HisobHarakati { OperatorId = op["alisher"].Id, Sana = T(10, 3, 8, 0), Turi = HarakatTuri.Kamomat, Summa = -120_000, Izoh = "Smena #40 yopilishida kassada naqd kam chiqdi", KimYozdi = "Tizim" });
        harakatlar.Add(new HisobHarakati { OperatorId = op["dilshod"].Id, Sana = T(10, 2, 8, 1), Turi = HarakatTuri.Ortiqcha, Summa = 20_000, Izoh = "Smena #39 yopilishida kassada naqd ortiqcha chiqdi", KimYozdi = "Tizim" });
        A(T(10, 2, 14, 10), Boshliq, "Avans berildi", AuditMatnlari.Avans("Alisher Karimov", 1_000_000, "naqd"), AuditTurlari.Hisob);
        A(T(10, 3, 9, 12), Boshliq, "Ko'rsatkich tuzatildi", AuditMatnlari.KorsatkichTuzatildi(40, 3, 62_949.60m, 62_946.90m, "yozishda xato"), AuditTurlari.Tuzatish);

        db.Smenalar.AddRange(smenalar);
        db.SmenaKorsatkichlari.AddRange(segmentlar);
        db.Nasiyalar.AddRange(nasiyalar);
        db.NasiyaQaytishlari.AddRange(qaytishlar);
        db.Xarajatlar.AddRange(xarajatlar);
        db.BakKirimlari.AddRange(kirimlar);
        db.Harakatlar.AddRange(harakatlar);
        db.Audit.AddRange(audit);
        await db.SaveChangesAsync();
        log.LogInformation("Demo ma'lumot (dizayn namunasi): {Smena} ta smena (#42 ochiq), {Segment} ta segment, {Nasiya} ta nasiya, {Xarajat} ta xarajat, {Kirim} ta bak kirimi.",
            smenalar.Count, segmentlar.Count, nasiyalar.Count, xarajatlar.Count, kirimlar.Count);
    }
}
