using FuelControl.Contracts;
using FuelControl.Contracts.Dto;
using FuelControl.Core.Modellar;

namespace FuelControl.Core.Xizmatlar;

public static class NasiyaXizmati
{
    public const int MaksIsm = 100, MaksMashina = 20, MaksIzoh = 500;

    /// <summary>Mijozlar takliflari ro'yxatining eng ko'p uzunligi.</summary>
    public const int MaksTaklif = 8;

    public static Nasiya Yarat(int smenaId, int operatorId, string kim, string? mijozIsmi, string? telefon, string? mashinaRaqami,
        long summa, DateOnly muddat, string? izoh, DateTime vaqtUtc)
    {
        if (string.IsNullOrWhiteSpace(mijozIsmi)) throw new ArgumentException("Mijoz ismi kiritilishi shart.");
        if (summa <= 0) throw new ArgumentException("Nasiya summasi musbat bo'lishi kerak.");
        // Telefon doim "+998 XX XXX XX XX" ko'rinishida saqlanadi; berilgan bo'lsa aynan 9 raqam bo'lishi shart (aks holda ArgumentException).
        var tel = TelefonRaqami.Normallashtir(telefon);
        // Qarzni undirish uchun mijozni topish kerak: telefon yoki mashina raqamidan kamida bittasi.
        if (tel.Length == 0 && string.IsNullOrWhiteSpace(mashinaRaqami))
            throw new ArgumentException("Telefon yoki mashina raqamidan kamida bittasi kiritilishi shart.");
        if (muddat < Vaqt.Sana(vaqtUtc)) throw new ArgumentException("Qaytarish muddati bugundan oldin bo'lishi mumkin emas.");
        if (mijozIsmi.Trim().Length > MaksIsm || (mashinaRaqami?.Trim().Length ?? 0) > MaksMashina || (izoh?.Trim().Length ?? 0) > MaksIzoh)
            throw new ArgumentException("Ism, mashina raqami yoki izoh juda uzun.");
        return new Nasiya
        {
            SmenaId = smenaId, OperatorId = operatorId, KimYozdi = kim, MijozIsmi = mijozIsmi.Trim(),
            Telefon = tel, MashinaRaqami = mashinaRaqami?.Trim() ?? "",
            Summa = summa, Muddat = muddat, Yozildi = vaqtUtc, Izoh = string.IsNullOrWhiteSpace(izoh) ? null : izoh.Trim(),
        };
    }

    /// <summary>
    /// Qarz qaytishi (to'liq yoki qisman): summa musbat va qoldiqdan oshmasligi kerak. smenaId - smena hisobiga yozilsa o'sha ochiq smena,
    /// aks holda null (smena hisobiga ta'sir qilmaydi).
    /// </summary>
    public static NasiyaQaytishi Qaytish(Nasiya n, long summa, TolovTuri usul, int? smenaId, int operatorId, string kim, string? izoh, DateTime vaqtUtc)
    {
        if (!Enum.IsDefined(usul)) throw new ArgumentException("To'lov usuli noto'g'ri.");
        if (summa <= 0) throw new ArgumentException("Qaytarilgan summa musbat bo'lishi kerak.");
        if (summa > n.Qoldiq) throw new ArgumentException($"Qaytarilgan summa qoldiqdan ({n.Qoldiq}) oshmasligi kerak.");
        n.Qaytgan += summa;
        if (n.Qoldiq == 0) n.Yopildi = vaqtUtc;
        return new NasiyaQaytishi
        {
            NasiyaId = n.Id, SmenaId = smenaId, Summa = summa, Usul = usul, Vaqt = vaqtUtc, OperatorId = operatorId, KimYozdi = kim,
            Izoh = string.IsNullOrWhiteSpace(izoh) ? null : izoh.Trim(),
        };
    }

    /// <summary>Xato yozilgan qaytishni olib tashlash: qoldiq tiklanadi, nasiya qayta ochiladi.</summary>
    public static void QaytishniOlibTashla(Nasiya n, NasiyaQaytishi q)
    {
        if (q.Summa > n.Qaytgan) throw new InvalidOperationException("Nasiya qaytishlari yig'indisi mos emas.");
        n.Qaytgan -= q.Summa;
        if (n.Qoldiq > 0) n.Yopildi = null;
    }

    /// <summary>Yopilgan - qoldiq 0; MuddatiOtgan - qoldiq bor va muddat (Toshkent sanasi) bugundan oldin; aks holda Faol.</summary>
    public static NasiyaHolati Holat(Nasiya n, DateOnly bugun) =>
        n.Qoldiq <= 0 ? NasiyaHolati.Yopilgan : n.Muddat < bugun ? NasiyaHolati.MuddatiOtgan : NasiyaHolati.Faol;

    /// <summary>Qidiruv qoidasi (ism / telefon / mashina raqami) - <see cref="NasiyaQidiruvi"/>; bo'sh so'rovga hamma nasiya mos.</summary>
    public static bool QidiruvgaMos(Nasiya n, string? q) => NasiyaQidiruvi.Tayyorla(q).Daraja(n) is not null;

    /// <summary>
    /// Nasiyalar ro'yxati: ?holat va ?q bo'yicha saralanadi. So'rovsiz - qarzi borlar muddat bo'yicha (muddati o'tgani birinchi), keyin yopilganlar;
    /// so'rov bilan - aynan va boshidan mos kelganlar oldin, keyin qolganlari, har bir guruhda yangisi oldinda.
    /// </summary>
    public static List<Nasiya> Royxat(IEnumerable<Nasiya> nasiyalar, string? holat, string? q, DateOnly bugun)
    {
        var sorov = NasiyaQidiruvi.Tayyorla(q);
        var mos = nasiyalar.Where(n => HolatFiltri(holat, n, bugun)).Select(n => (N: n, D: sorov.Daraja(n))).Where(x => x.D is not null).ToList();
        return (sorov.Bosh
                ? mos.OrderBy(x => x.N.Qoldiq > 0 ? 0 : 1).ThenBy(x => x.N.Muddat).ThenByDescending(x => x.N.Id)
                : mos.OrderBy(x => x.D).ThenByDescending(x => x.N.Yozildi).ThenByDescending(x => x.N.Id))
            .Select(x => x.N).ToList();
    }

    /// <summary>
    /// Mavjud mijozlar (avtomatik to'ldirish): nasiya yozuvlaridan yig'iladi - telefon bo'lsa telefon bo'yicha, bo'lmasa ism va mashina raqami
    /// bo'yicha guruhlanadi. Qidiruv qoidasi <see cref="Royxat"/> bilan bir xil; tartib: aynan/boshidan mos kelganlar oldin, yangisi oldinda.
    /// Taklifdagi ism/mashina raqami - so'rovga eng yaxshi mos (keyin eng yangi) yozuvdan, ya'ni eski mashina raqami bo'yicha topilsa o'sha raqam
    /// taklif qilinadi; NasiyaSoni va FaolQarz esa mijozning hamma nasiyalari bo'yicha. So'rov bo'sh bo'lsa - eng oxirgi mijozlar.
    /// </summary>
    public static MijozTaklifDto[] MijozTakliflari(IEnumerable<Nasiya> nasiyalar, string? q, int maks = MaksTaklif)
    {
        var sorov = NasiyaQidiruvi.Tayyorla(q);
        var topilgan = new List<(Nasiya Vakil, int Daraja, Nasiya Oxirgi, int Soni, long Qarz)>();
        foreach (var guruh in nasiyalar.GroupBy(MijozKaliti))
        {
            var yozuvlar = guruh.OrderByDescending(n => n.Yozildi).ThenByDescending(n => n.Id).ToList();     // yangisi birinchi
            var vakil = yozuvlar.Select(n => (N: n, D: sorov.Daraja(n))).Where(x => x.D is not null).OrderBy(x => x.D).FirstOrDefault();
            if (vakil.N is null) continue;
            topilgan.Add((vakil.N, vakil.D!.Value, yozuvlar[0], yozuvlar.Count, yozuvlar.Sum(n => n.Qoldiq)));
        }
        return topilgan.OrderBy(x => x.Daraja).ThenByDescending(x => x.Oxirgi.Yozildi).ThenByDescending(x => x.Oxirgi.Id).Take(maks)
            .Select(x => new MijozTaklifDto(x.Vakil.MijozIsmi, x.Vakil.Telefon, x.Vakil.MashinaRaqami, x.Soni, x.Qarz, Vaqt.Sana(x.Oxirgi.Yozildi)))
            .ToArray();
    }

    /// <summary>Mijoz kaliti: telefon bo'lsa (mamlakat kodisiz raqamlar), bo'lmasa ism + mashina raqami (katta-kichik harf, apostrof va ortiqcha bo'shliqlarsiz).</summary>
    private static string MijozKaliti(Nasiya n)
    {
        var telefon = TelefonRaqami.Milliy(n.Telefon);
        return telefon.Length > 0 ? "t:" + telefon : "n:" + NasiyaQidiruvi.IsmKaliti(n.MijozIsmi) + "|" + NasiyaQidiruvi.RaqamKaliti(n.MashinaRaqami);
    }

    /// <summary>Muddat - bugun (kun); manfiy = muddat o'tgan.</summary>
    public static int MuddatgachaKun(Nasiya n, DateOnly bugun) => n.Muddat.DayNumber - bugun.DayNumber;

    /// <summary>?holat=faol|otgan|yopilgan: faol - qarzi bor hammasi (muddati o'tganlar ham), otgan - faqat muddati o'tgan, yopilgan - qoldiq 0. Bo'sh = hammasi.</summary>
    public static bool HolatFiltri(string? holat, Nasiya n, DateOnly bugun) => holat?.Trim().ToLowerInvariant() switch
    {
        null or "" => true,
        "faol" => n.Qoldiq > 0,
        "otgan" => Holat(n, bugun) == NasiyaHolati.MuddatiOtgan,
        "yopilgan" => n.Qoldiq <= 0,
        _ => throw new ArgumentException("holat faqat faol, otgan yoki yopilgan bo'lishi mumkin."),
    };

    /// <summary>Nasiyalar sahifasi va Boshqaruv uchun yig'indilar. Oy* - Toshkent oyi chegaralari (UTC).</summary>
    public static NasiyalarXulosaDto Xulosa(IReadOnlyCollection<Nasiya> nasiyalar, IReadOnlyCollection<NasiyaQaytishi> qaytishlar,
        DateOnly bugun, DateTime oyBoshiUtc, DateTime keyingiOyBoshiUtc)
    {
        var qarzdorlar = nasiyalar.Where(n => n.Qoldiq > 0).ToList();
        var otgan = qarzdorlar.Where(n => n.Muddat < bugun).ToList();
        var oyBerilgan = nasiyalar.Where(n => n.Yozildi >= oyBoshiUtc && n.Yozildi < keyingiOyBoshiUtc).ToList();
        var oyQaytgan = qaytishlar.Where(q => q.Vaqt >= oyBoshiUtc && q.Vaqt < keyingiOyBoshiUtc).ToList();
        return new NasiyalarXulosaDto(qarzdorlar.Sum(n => n.Qoldiq), qarzdorlar.Count, otgan.Sum(n => n.Qoldiq), otgan.Count,
            oyBerilgan.Sum(n => n.Summa), oyBerilgan.Count, oyQaytgan.Sum(q => q.Summa), oyQaytgan.Count);
    }
}
