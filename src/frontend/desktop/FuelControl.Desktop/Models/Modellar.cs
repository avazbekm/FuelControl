using System;
using System.Collections.Generic;
using System.Linq;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Services;

namespace FuelControl.Desktop.Models;

// Rol, TolovTuri, XarajatManbai, NasiyaHolati, HarakatTuri, Ruxsat — FuelControl.Contracts dan (server bilan umumiy).

public static class Ruxsatlar
{
    /// <summary>(Ruxsat, Guruh) — B: bo'lim (menyu), A: amal. Nomi "Ruxsat_" + ruxsat, izohi "Ruxsat_" + ruxsat + "_Izoh".</summary>
    public static readonly (Ruxsat Ruxsat, string Guruh)[] Royxat =
    [
        (Ruxsat.Boshqaruv, "B"), (Ruxsat.Savdo, "B"), (Ruxsat.Smenalar, "B"), (Ruxsat.Nasiyalar, "B"), (Ruxsat.Hisobotlar, "B"),
        (Ruxsat.Operatorlar, "B"), (Ruxsat.Audit, "B"), (Ruxsat.Sozlamalar, "B"),
        (Ruxsat.SmenaOchish, "A"), (Ruxsat.SmenaYopish, "A"), (Ruxsat.NasiyaYozish, "A"), (Ruxsat.QarzQaytdi, "A"),
        (Ruxsat.XarajatYozish, "A"), (Ruxsat.BakKirim, "A"), (Ruxsat.KorsatkichTuzatish, "A"), (Ruxsat.AvansBerish, "A"), (Ruxsat.Eksport, "A"),
    ];

    /// <summary>Smena hisobi modelida qo'shilgan ruxsatlar (Sozlamalar'da "Yangi" belgisi).</summary>
    public static readonly HashSet<Ruxsat> Yangilar =
        [Ruxsat.Savdo, Ruxsat.Nasiyalar, Ruxsat.NasiyaYozish, Ruxsat.QarzQaytdi, Ruxsat.XarajatYozish, Ruxsat.BakKirim, Ruxsat.KorsatkichTuzatish];

    /// <summary>Rol bo'yicha boshlang'ich ruxsatlar (server bilan bir xil). Keyin Admin har foydalanuvchiga alohida o'zgartiradi.</summary>
    public static HashSet<Ruxsat> Standart(Rol rol) => rol switch
    {
        Rol.Operator => [Ruxsat.Savdo, Ruxsat.Nasiyalar, Ruxsat.SmenaOchish, Ruxsat.SmenaYopish, Ruxsat.NasiyaYozish, Ruxsat.QarzQaytdi, Ruxsat.XarajatYozish],
        Rol.Boshliq => [.. Enum.GetValues<Ruxsat>().Where(r => r != Ruxsat.Sozlamalar)],
        _ => [.. Enum.GetValues<Ruxsat>()],
    };

    public static string Nomi(Ruxsat r) => Til.T("Ruxsat_" + r);
    public static string Izoh(Ruxsat r) => Til.Bormi("Ruxsat_" + r + "_Izoh") ? Til.T("Ruxsat_" + r + "_Izoh") : "";
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

    /// <summary>API'dagi "boshliq" = Smenalar ruxsati bor: boshqalar yozuvini o'chiradi, boshqa operator smenasini yopadi.</summary>
    public bool Boshliqmi => Bor(Ruxsat.Smenalar);

    public string RolNomi => Til.T("Rol_" + Rol);

    /// <summary>Avatar uchun bosh harflar: "Narimonjon Abdullayev" → "NA".</summary>
    public string BoshHarflar => Format.BoshHarflar(ToliqIsm);
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
    /// <summary>Pultdagi "Total, L" — oxirgi yopilgan smena holatida.</summary>
    public decimal TotalLitr { get; set; }
    /// <summary>Aparatning o'z baki, litrda (manfiy bo'lishi mumkin — kirim yozilmay qolgan).</summary>
    public decimal BakQoldiq { get; set; }
    public DateTime? OxirgiKirimVaqti { get; set; }
    public decimal? OxirgiKirimLitr { get; set; }

    public bool BakManfiy => BakQoldiq < 0;
}

/// <summary>Smena (SmenaDto ning lokal ko'rinishi; vaqtlar mahalliy vaqtda).</summary>
public sealed class Smena
{
    public int Id { get; init; }
    public Foydalanuvchi Operator { get; init; } = null!;
    public DateTime Boshlandi { get; set; }
    public DateTime? Tugadi { get; set; }

    public long OchishQaytim { get; set; }
    public long OchishTerminal { get; set; }
    public long OchishDepozit { get; set; }
    public long? YopishTerminal { get; set; }
    public long? YopishDepozit { get; set; }
    public long? SanalganNaqd { get; set; }

    public decimal JamiLitr { get; set; }
    public long Savdo { get; set; }
    public long Plastik { get; set; }
    public long DepozitFarqi { get; set; }
    public long NasiyaJami { get; set; }
    public long QaytganNasiya { get; set; }
    public long XarajatJami { get; set; }
    public long Kutilgan { get; set; }
    /// <summary>Manfiy = kamomat, musbat = ortiqcha (ochiq smenada 0).</summary>
    public long Farq { get; set; }
    public string? Izoh { get; set; }

    public bool Ochiqmi => Tugadi is null;
    public long Kamomat => Farq < 0 ? -Farq : 0;
    public bool KamomatBormi => Farq < 0;
    public bool OrtiqchaBormi => Farq > 0;
    public bool FarqYoq => !Ochiqmi && Farq == 0;
    public string FarqMatni => Format.Farq(Farq);
    public string Davomiylik => Format.Davomiylik((Tugadi ?? DateTime.Now) - Boshlandi);
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
    /// <summary>"smena" | "nasiya" | "xarajat" | "bak" | "tuzatish" | "hisob" | "sozlama" | "kirish".</summary>
    public string Tur { get; init; } = "";
}

public sealed class NarxTarixi
{
    public DateTime Vaqt { get; init; }
    public string Yoqilgi { get; init; } = "";
    public long EskiNarx { get; init; }
    public long YangiNarx { get; init; }
    public string Kim { get; init; } = "";
}
