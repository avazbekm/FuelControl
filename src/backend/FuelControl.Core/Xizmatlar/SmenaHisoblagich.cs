using FuelControl.Contracts;
using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

/// <summary>
/// Smena hisobi — yagona haqiqat manbai (klientlar jonli ko'rsatish uchun aynan shu formulani takrorlaydi). Hammasi sof mantiq:
/// bazaga yozish va tranzaksiya Api'da; bu yerda faqat berilgan obyektlar o'zgartiriladi.
/// <code>
/// Litr  = Oxiri - Boshi (2 xona);  Summa = round(Litr x Narx) so'mgacha (AwayFromZero)
/// Savdo = sum(Summa);  Plastik = YopishTerminal - OchishTerminal;  DepozitFarqi = YopishDepozit - OchishDepozit (manfiy bo'lishi mumkin)
/// Kutilgan = Qaytim + Savdo + QaytganNasiya - Plastik - DepozitFarqi - NasiyaJami - XarajatJami;  Farq = SanalganNaqd - Kutilgan
/// </code>
/// </summary>
public static class SmenaHisoblagich
{
    public sealed record Natija(decimal JamiLitr, long Savdo, long Plastik, long DepozitFarqi,
        long NasiyaJami, long QaytganNasiya, long XarajatJami, long Kutilgan, long Farq);

    /// <summary>Smena davomida yozilgan nasiya, smena hisobiga yozilgan qaytishlar va xarajatlar yig'indisi.</summary>
    public sealed record Yigindilar(long NasiyaJami, long QaytganNasiya, long XarajatJami)
    {
        public static readonly Yigindilar Bosh = new(0, 0, 0);
    }

    public sealed record YopishNatijasi(IReadOnlyList<SmenaKorsatkichi> YangiSegmentlar, Natija Natija, HisobHarakati? Harakat);

    public sealed record TuzatishNatijasi(Natija Eski, Natija Yangi, decimal EskiOxiri, decimal YangiOxiri, HisobHarakati? Harakat);

    public static decimal Yaxlitla(decimal qiymat) => Math.Round(qiymat, 2, MidpointRounding.AwayFromZero);

    public static decimal Litr(decimal boshi, decimal oxiri) => Yaxlitla(oxiri - boshi);

    public static long Summa(decimal litr, long narx) => (long)Math.Round(litr * narx, 0, MidpointRounding.AwayFromZero);

    public static Yigindilar SmenaYigindilari(int smenaId, IEnumerable<Nasiya> nasiyalar,
        IEnumerable<NasiyaQaytishi> qaytishlar, IEnumerable<Xarajat> xarajatlar) =>
        new(nasiyalar.Where(n => n.SmenaId == smenaId).Sum(n => n.Summa),
            qaytishlar.Where(q => q.SmenaId == smenaId).Sum(q => q.Summa),
            xarajatlar.Where(x => x.SmenaId == smenaId).Sum(x => x.Summa));

    /// <summary>Formula. Segmentlar — smenaning hamma aparat segmentlari.</summary>
    public static Natija Hisobla(Smena s, long yopishTerminal, long yopishDepozit, long sanalganNaqd,
        IEnumerable<SmenaKorsatkichi> segmentlar, Yigindilar y)
    {
        var royxat = segmentlar.ToList();
        var savdo = royxat.Sum(x => x.Summa);
        var plastik = yopishTerminal - s.OchishTerminal;
        var depozitFarqi = yopishDepozit - s.OchishDepozit;
        var kutilgan = s.OchishQaytim + savdo + y.QaytganNasiya - plastik - depozitFarqi - y.NasiyaJami - y.XarajatJami;
        return new Natija(royxat.Sum(x => x.Litr), savdo, plastik, depozitFarqi,
            y.NasiyaJami, y.QaytganNasiya, y.XarajatJami, kutilgan, sanalganNaqd - kutilgan);
    }

    public static void Qollash(Smena s, Natija n)
    {
        s.JamiLitr = n.JamiLitr; s.Savdo = n.Savdo; s.Plastik = n.Plastik; s.DepozitFarqi = n.DepozitFarqi;
        s.NasiyaJami = n.NasiyaJami; s.QaytganNasiya = n.QaytganNasiya; s.XarajatJami = n.XarajatJami;
        s.Kutilgan = n.Kutilgan; s.Farq = n.Farq;
    }

    /// <summary>Yopilgan smenaning saqlangan natijasi.</summary>
    public static Natija Saqlangan(Smena s) => new(s.JamiLitr, s.Savdo, s.Plastik, s.DepozitFarqi,
        s.NasiyaJami, s.QaytganNasiya, s.XarajatJami, s.Kutilgan, s.Farq);

    public static Smena Och(int operatorId, long qaytim, long terminal, long depozit, DateTime vaqtUtc)
    {
        if (qaytim < 0 || terminal < 0 || depozit < 0)
            throw new ArgumentException("Qaytim, terminal va depozit manfiy bo'lishi mumkin emas.");
        return new Smena
        {
            OperatorId = operatorId, Boshlandi = vaqtUtc,
            OchishQaytim = qaytim, OchishTerminal = terminal, OchishDepozit = depozit,
        };
    }

    /// <summary>
    /// Smena ichida aparatning navbatdagi segmenti qayerdan boshlanadi: qayd etilgan (narx o'zgarishi) segment bo'lsa oxirgisining Oxiri,
    /// aks holda aparatning TotalLitr'i (oxirgi yopilgan smena holati).
    /// </summary>
    public static decimal Oldingi(Aparat aparat, IEnumerable<SmenaKorsatkichi> aparatSegmentlari) =>
        aparatSegmentlari.OrderByDescending(x => x.Tartib).FirstOrDefault()?.Oxiri ?? aparat.TotalLitr;

    private static SmenaKorsatkichi YangiSegment(int smenaId, Aparat a, int tartib, decimal boshi, decimal oxiri, long narx, bool narxOzgarishida, DateTime vaqt)
    {
        var litr = Litr(boshi, oxiri);
        return new SmenaKorsatkichi
        {
            SmenaId = smenaId, AparatId = a.Id, Tartib = tartib, Boshi = boshi, Oxiri = oxiri, Narx = narx,
            Litr = litr, Summa = Summa(litr, narx), NarxOzgarishida = narxOzgarishida, Vaqt = vaqt,
        };
    }

    private static ArgumentException KichikKorsatkich(Aparat a, decimal oldingi) =>
        new($"{a.Raqam}-aparat: yangi pult ko'rsatkichi oldingi ko'rsatkich ({Format.Litr(oldingi)}) dan kichik bo'lishi mumkin emas.");

    /// <summary>
    /// Ochiq smenada yoqilg'i narxi o'zgarsa: shu yoqilg'i barcha aparatlarining hozirgi pult ko'rsatkichi majburiy (yetishmasa
    /// <see cref="KorsatkichKerakXatosi"/>) va oldingisidan kichik bo'lmasligi shart. Shu paytgacha bo'lgan litr eski narxda alohida
    /// segment (NarxOzgarishida = true) bo'ladi; litr 0 bo'lsa segment yozilmaydi. Qolgani yopishda yangi narxda hisoblanadi.
    /// </summary>
    public static List<SmenaKorsatkichi> NarxOzgarishi(Smena smena, long eskiNarx, IReadOnlyCollection<Aparat> aparatlar,
        IReadOnlyCollection<SmenaKorsatkichi> mavjud, IReadOnlyDictionary<int, decimal> korsatkichlar, DateTime vaqtUtc)
    {
        if (!smena.Ochiqmi) throw new InvalidOperationException("Smena yopilgan.");
        var yetishmaydi = aparatlar.Where(a => !korsatkichlar.ContainsKey(a.Id)).Select(a => a.Id).ToArray();
        if (yetishmaydi.Length > 0) throw new KorsatkichKerakXatosi(yetishmaydi);
        var idlar = aparatlar.Select(a => a.Id).ToHashSet();
        if (korsatkichlar.Keys.Any(id => !idlar.Contains(id)))
            throw new ArgumentException("Ko'rsatkich faqat shu yoqilg'i aparatlari uchun beriladi.");

        var smenaSegmentlari = mavjud.Where(x => x.SmenaId == smena.Id).ToList();
        var yangi = new List<SmenaKorsatkichi>();
        foreach (var a in aparatlar.OrderBy(x => x.Raqam))
        {
            var segmentlari = smenaSegmentlari.Where(x => x.AparatId == a.Id).ToList();
            var oldingi = Oldingi(a, segmentlari);
            var q = Yaxlitla(korsatkichlar[a.Id]);
            if (q < oldingi) throw KichikKorsatkich(a, oldingi);
            if (q == oldingi) continue;
            var tartib = segmentlari.Select(x => x.Tartib).DefaultIfEmpty(0).Max() + 1;
            yangi.Add(YangiSegment(smena.Id, a, tartib, oldingi, q, eskiNarx, true, vaqtUtc));
        }
        return yangi;
    }

    /// <summary>
    /// Smenani yopadi. Har aparat uchun pult ko'rsatkichi majburiy va oldingisidan kichik bo'lmasligi shart; oxirgi segment joriy narxda
    /// (yoqilgiNarxlari: YoqilgiTuriId -> narx). Aparatning TotalLitr'i yangi ko'rsatkichga teng bo'ladi, bak qoldig'idan smenada sotilgan
    /// litr ayriladi. Kamomat/ortiqcha bo'lsa operator hisobiga yoziladigan harakat qaytariladi (Smena #N).
    /// </summary>
    public static YopishNatijasi Yop(Smena smena, IReadOnlyCollection<Aparat> aparatlar, IReadOnlyDictionary<int, long> yoqilgiNarxlari,
        IReadOnlyCollection<SmenaKorsatkichi> mavjud, IReadOnlyDictionary<int, decimal> korsatkichlar,
        long terminal, long depozit, long sanalganNaqd, string? izoh, Yigindilar y, DateTime vaqtUtc)
    {
        if (!smena.Ochiqmi) throw new InvalidOperationException("Smena allaqachon yopilgan.");
        if (terminal < 0 || depozit < 0 || sanalganNaqd < 0)
            throw new ArgumentException("Terminal, depozit va sanalgan naqd manfiy bo'lishi mumkin emas.");
        var yetishmaydi = aparatlar.Where(a => !korsatkichlar.ContainsKey(a.Id)).Select(a => a.Raqam).OrderBy(x => x).ToList();
        if (yetishmaydi.Count > 0)
            throw new ArgumentException($"Barcha aparatlarning pult ko'rsatkichi majburiy. Yetishmaydi: {string.Join(", ", yetishmaydi)}-aparat.");
        var idlar = aparatlar.Select(a => a.Id).ToHashSet();
        if (korsatkichlar.Keys.Any(id => !idlar.Contains(id))) throw new ArgumentException("Noma'lum aparat uchun ko'rsatkich berilgan.");

        var smenaSegmentlari = mavjud.Where(x => x.SmenaId == smena.Id).ToList();
        var yangi = new List<SmenaKorsatkichi>();
        foreach (var a in aparatlar.OrderBy(x => x.Raqam))
        {
            var segmentlari = smenaSegmentlari.Where(x => x.AparatId == a.Id).ToList();
            var oldingi = Oldingi(a, segmentlari);
            var q = Yaxlitla(korsatkichlar[a.Id]);
            if (q < oldingi) throw KichikKorsatkich(a, oldingi);
            var tartib = segmentlari.Select(x => x.Tartib).DefaultIfEmpty(0).Max() + 1;
            yangi.Add(YangiSegment(smena.Id, a, tartib, oldingi, q, yoqilgiNarxlari[a.YoqilgiTuriId], false, vaqtUtc));
        }

        var barcha = smenaSegmentlari.Concat(yangi).ToList();
        var natija = Hisobla(smena, terminal, depozit, sanalganNaqd, barcha, y);

        // Hamma tekshiruvdan keyingina o'zgartiramiz.
        smena.Tugadi = vaqtUtc;
        smena.YopishTerminal = terminal; smena.YopishDepozit = depozit; smena.SanalganNaqd = sanalganNaqd;
        smena.Izoh = string.IsNullOrWhiteSpace(izoh) ? null : izoh.Trim();
        Qollash(smena, natija);
        foreach (var a in aparatlar)
        {
            a.BakQoldiq -= barcha.Where(x => x.AparatId == a.Id).Sum(x => x.Litr);
            a.TotalLitr = Yaxlitla(korsatkichlar[a.Id]);
        }
        return new YopishNatijasi(yangi, natija, FarqHarakati(smena, natija.Farq, vaqtUtc));
    }

    /// <summary>Kamomat (manfiy farq) yoki ortiqcha (musbat) operator hisobiga: Summa farqning o'zi (kamomat manfiy). Farq 0 bo'lsa - yo'q.</summary>
    public static HisobHarakati? FarqHarakati(Smena s, long farq, DateTime vaqtUtc) =>
        farq == 0 ? null : new HisobHarakati
        {
            OperatorId = s.OperatorId, Sana = vaqtUtc,
            Turi = farq < 0 ? HarakatTuri.Kamomat : HarakatTuri.Ortiqcha,
            Summa = farq, Izoh = $"Smena #{s.Id}", KimYozdi = "Tizim",
        };

    /// <summary>
    /// Oxirgi yopilgan smenada aparat ko'rsatkichini tuzatadi (chaqiruvchi shu smena oxirgi yopilgani va ruxsatni tekshiradi). Smena qayta
    /// hisoblanadi; farq o'zgarishi uchun tuzatuvchi harakat qaytariladi; aparat TotalLitr'i va bak qoldig'i delta bo'yicha tuzatiladi;
    /// keyingi (ochiq) smenada narx o'zgarishida qayd etilgan birinchi segmentning boshlanishi ham yangilanadi.
    /// </summary>
    public static TuzatishNatijasi KorsatkichniTuzat(Smena smena, Aparat aparat, IReadOnlyCollection<SmenaKorsatkichi> smenaSegmentlari,
        IReadOnlyCollection<SmenaKorsatkichi> keyingiSmenaSegmentlari, decimal qiymat, string sabab, string kim, DateTime vaqtUtc)
    {
        if (smena.Ochiqmi) throw new InvalidOperationException("Ochiq smena ko'rsatkichini tuzatib bo'lmaydi: avval smenani yoping.");
        if (string.IsNullOrWhiteSpace(sabab)) throw new ArgumentException("Tuzatish sababi majburiy.");
        var q = Yaxlitla(qiymat);
        var segment = smenaSegmentlari.Where(x => x.AparatId == aparat.Id).OrderByDescending(x => x.Tartib).FirstOrDefault()
            ?? throw new ArgumentException($"{aparat.Raqam}-aparat bu smenada qatnashmagan.");
        var eskiOxiri = segment.Oxiri;
        if (q == eskiOxiri) throw new ArgumentException("Yangi ko'rsatkich joriy ko'rsatkichdan farq qilishi kerak.");
        if (q < segment.Boshi) throw KichikKorsatkich(aparat, segment.Boshi);
        var keyingi = keyingiSmenaSegmentlari.Where(x => x.AparatId == aparat.Id).OrderBy(x => x.Tartib).FirstOrDefault();
        if (keyingi is not null && q > keyingi.Oxiri)
            throw new ArgumentException($"{aparat.Raqam}-aparat: ko'rsatkich keyingi (ochiq) smenadagi narx o'zgarishi ko'rsatkichi ({Format.Litr(keyingi.Oxiri)}) dan katta bo'lishi mumkin emas.");

        var eski = Saqlangan(smena);
        var eskiLitr = segment.Litr;
        segment.Oxiri = q;
        segment.Litr = Litr(segment.Boshi, q);
        segment.Summa = Summa(segment.Litr, segment.Narx);
        if (keyingi is not null)
        {
            keyingi.Boshi = q;
            keyingi.Litr = Litr(q, keyingi.Oxiri);
            keyingi.Summa = Summa(keyingi.Litr, keyingi.Narx);
        }

        var yangi = Hisobla(smena, smena.YopishTerminal ?? 0, smena.YopishDepozit ?? 0, smena.SanalganNaqd ?? 0, smenaSegmentlari,
            new Yigindilar(smena.NasiyaJami, smena.QaytganNasiya, smena.XarajatJami));
        Qollash(smena, yangi);
        aparat.TotalLitr = Yaxlitla(aparat.TotalLitr + (q - eskiOxiri));
        aparat.BakQoldiq -= segment.Litr - eskiLitr;

        var delta = yangi.Farq - eski.Farq;
        HisobHarakati? harakat = delta == 0 ? null : new HisobHarakati
        {
            OperatorId = smena.OperatorId, Sana = vaqtUtc,
            Turi = delta < 0 ? HarakatTuri.Kamomat : HarakatTuri.Ortiqcha, Summa = delta,
            Izoh = $"Smena #{smena.Id} tuzatish: {aparat.Raqam}-aparat {Format.Litr(eskiOxiri)} dan {Format.Litr(q)} ga. Sabab: {sabab.Trim()}",
            KimYozdi = kim,
        };
        return new TuzatishNatijasi(eski, yangi, eskiOxiri, q, harakat);
    }
}
