using System;
using System.Collections.Generic;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.Models;

// Rol, TolovTuri, SotuvHolati, HarakatTuri, Ruxsat — FuelControl.Contracts dan (server bilan umumiy).

public static class Ruxsatlar
{
    /// <summary>(Ruxsat, Guruh) — nomi va izohi Til orqali: "R_" + ruxsat, "RI_" + ruxsat.</summary>
    public static readonly (Ruxsat Ruxsat, string Guruh)[] Royxat =
    [
        (Ruxsat.Boshqaruv, "B"), (Ruxsat.SotuvKiritish, "B"), (Ruxsat.Smenalar, "B"), (Ruxsat.Hisobotlar, "B"),
        (Ruxsat.Operatorlar, "B"), (Ruxsat.Audit, "B"), (Ruxsat.Sozlamalar, "B"),
        (Ruxsat.SmenaOchish, "A"), (Ruxsat.SmenaYopish, "A"), (Ruxsat.SotuvTahrirlash, "A"), (Ruxsat.SotuvBekorQilish, "A"), (Ruxsat.AvansBerish, "A"), (Ruxsat.Eksport, "A"),
    ];

    /// <summary>Rol bo'yicha boshlang'ich ruxsatlar. Keyin Admin har foydalanuvchiga alohida o'zgartiradi.</summary>
    public static HashSet<Ruxsat> Standart(Rol rol) => rol switch
    {
        Rol.Operator => [Ruxsat.SotuvKiritish, Ruxsat.SmenaOchish, Ruxsat.SmenaYopish],
        Rol.Boshliq => [Ruxsat.Boshqaruv, Ruxsat.SotuvKiritish, Ruxsat.Smenalar, Ruxsat.Hisobotlar, Ruxsat.Operatorlar, Ruxsat.Audit,
                        Ruxsat.SmenaOchish, Ruxsat.SmenaYopish, Ruxsat.SotuvTahrirlash, Ruxsat.SotuvBekorQilish, Ruxsat.AvansBerish, Ruxsat.Eksport],
        _ => [.. System.Enum.GetValues<Ruxsat>()],
    };
}

public sealed class Foydalanuvchi
{
    public int Id { get; init; }
    public string ToliqIsm { get; set; } = "";
    public string Login { get; set; } = "";
    public Rol Rol { get; set; }
    public bool Faol { get; set; } = true;
    public long OylikMaosh { get; set; }
    public HashSet<Ruxsat> Ruxsatlar { get; set; } = new();

    public bool Bor(Ruxsat r) => Ruxsatlar.Contains(r);
    public int RuxsatSoni => Ruxsatlar.Count;

    public string RolNomi => Til.T("Rol_" + Rol);

    /// <summary>Avatar uchun bosh harflar: "Narimonjon Abdullayev" → "NA".</summary>
    public string BoshHarflar
    {
        get
        {
            var q = ToliqIsm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return q.Length >= 2 ? $"{q[0][0]}{q[1][0]}" : ToliqIsm.Length > 0 ? ToliqIsm[..1] : "?";
        }
    }
}

public sealed class YoqilgiTuri
{
    public int Id { get; init; }
    public string Nomi { get; set; } = "";
    public long Narx { get; set; }
    public string Rang { get; set; } = "#2F6BFF";
}

public sealed class Aparat
{
    public int Id { get; init; }
    public int Raqam { get; set; }
    public YoqilgiTuri Yoqilgi { get; set; } = null!;
    /// <summary>Totalizator — pultdagi "Total L": jami sotilgan litr (boshlang'ich + barcha sotuvlar).</summary>
    public decimal TotalLitr { get; set; }
}

public sealed class Tolov
{
    public TolovTuri Turi { get; init; }
    public long Summa { get; init; }
}

public sealed class Sotuv
{
    public int Id { get; init; }
    public int SmenaId { get; init; }
    public Foydalanuvchi Operator { get; init; } = null!;
    public Aparat Aparat { get; set; } = null!;
    public long Narx { get; set; }
    public decimal Litr { get; set; }
    public long Summa { get; set; }
    public DateTime Vaqt { get; init; }
    public List<Tolov> Tolovlar { get; set; } = new();
    public SotuvHolati Holati { get; set; } = SotuvHolati.Faol;
    public string? BekorSababi { get; set; }
    public string? BekorQilgan { get; set; }

    public string YoqilgiNomi => Aparat.Yoqilgi.Nomi;
    public long Naqd => Yigindi(TolovTuri.Naqd);
    public long Plastik => Yigindi(TolovTuri.Plastik);
    public long Click => Yigindi(TolovTuri.Click);
    public bool Faolmi => Holati == SotuvHolati.Faol;

    public string TolovNomi
    {
        get
        {
            if (Tolovlar.Count > 1) return Til.T("Aralash");
            return Tolovlar.Count == 0 ? "—" : Til.T(Tolovlar[0].Turi.ToString());
        }
    }

    private long Yigindi(TolovTuri t)
    {
        long s = 0;
        foreach (var p in Tolovlar) if (p.Turi == t) s += p.Summa;
        return s;
    }
}

public sealed class Smena
{
    public int Id { get; init; }
    public Foydalanuvchi Operator { get; init; } = null!;
    public DateTime Boshlandi { get; init; }
    public DateTime? Tugadi { get; set; }

    // Hisoblangan (sotuvlardan)
    public long KutilganNaqd { get; set; }
    public long KutilganPlastik { get; set; }
    public long KutilganClick { get; set; }
    public decimal JamiLitr { get; set; }
    public int SotuvSoni { get; set; }

    // Yopishda operator topshirgan
    public long? TopshirilganNaqd { get; set; }
    public long? TopshirilganPlastik { get; set; }
    public long? TopshirilganClick { get; set; }

    public bool Ochiqmi => Tugadi is null;
    public long KutilganJami => KutilganNaqd + KutilganPlastik + KutilganClick;
    public long TopshirilganJami => (TopshirilganNaqd ?? 0) + (TopshirilganPlastik ?? 0) + (TopshirilganClick ?? 0);
    /// <summary>Manfiy = kamomat, musbat = ortiqcha.</summary>
    public long Farq => Ochiqmi ? 0 : TopshirilganJami - KutilganJami;
    public long Kamomat => Farq < 0 ? -Farq : 0;
    public bool KamomatBormi => Farq < 0;
    public string Davomiylik
    {
        get
        {
            var oxiri = Tugadi ?? DateTime.Now;
            var d = oxiri - Boshlandi;
            return d.TotalHours >= 1 ? $"{(int)d.TotalHours} {Til.T("Soat")} {d.Minutes} {Til.T("Min")}" : $"{d.Minutes} {Til.T("Min")}";
        }
    }
}

public sealed class HisobHarakati
{
    public int Id { get; init; }
    public Foydalanuvchi Operator { get; init; } = null!;
    public DateTime Sana { get; init; }
    public HarakatTuri Turi { get; init; }
    /// <summary>Operator foydasiga musbat (maosh, ortiqcha), operator hisobidan manfiy (avans, kamomat).</summary>
    public long Summa { get; init; }
    public string Izoh { get; init; } = "";
    public string KimYozdi { get; init; } = "";

    public string TuriNomi => Til.T("H_" + Turi);
}

public sealed class AuditYozuvi
{
    public int Id { get; init; }
    public DateTime Vaqt { get; init; }
    public string Kim { get; init; } = "";
    public string Amal { get; init; } = "";
    public string Tafsilot { get; init; } = "";
}

public sealed class NarxTarixi
{
    public DateTime Vaqt { get; init; }
    public string Yoqilgi { get; init; } = "";
    public long EskiNarx { get; init; }
    public long YangiNarx { get; init; }
    public string Kim { get; init; } = "";
}
