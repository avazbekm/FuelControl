using System;
using System.Collections.Generic;
using System.Linq;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Models;

namespace FuelControl.Desktop.Services;

/// <summary>Smena yopish natijasi (§1.6): Farq manfiy — kamomat, musbat — ortiqcha.</summary>
public sealed record SmenaNatijasi(long Savdo, long Plastik, long DepozitFarqi, long Kutilgan, long Farq);

/// <summary>
/// Smena hisobi formulasi — server bilan aynan bir xil (server yagona haqiqat manbai, klient jonli ko'rsatish uchun hisoblaydi):
/// Litr = Oxiri − Boshi (2 xona), Summa = round(Litr × Narx) (AwayFromZero), Plastik = YopishTerminal − OchishTerminal,
/// DepozitFarqi = YopishDepozit − OchishDepozit, Kutilgan = Qaytim + Savdo + QaytganNasiya − Plastik − DepozitFarqi − Nasiya − Xarajat,
/// Farq = SanalganNaqd − Kutilgan.
/// </summary>
public static class SmenaHisobi
{
    public static decimal Litr(decimal boshi, decimal oxiri) => Math.Round(oxiri - boshi, 2, MidpointRounding.AwayFromZero);

    public static long Summa(decimal litr, long narx) => (long)Math.Round(litr * narx, 0, MidpointRounding.AwayFromZero);

    /// <summary>§7.1: ochiq smenada aparat uchun qayd etilgan segment bo'lsa — oxirgisining Oxiri, aks holda TotalLitr.</summary>
    public static decimal Oldingi(int aparatId, decimal totalLitr, IEnumerable<SmenaKorsatkichDto> qayd) =>
        qayd.LastOrDefault(k => k.AparatId == aparatId)?.Oxiri ?? totalLitr;

    public static decimal Oldingi(AparatDto a, IEnumerable<SmenaKorsatkichDto> qayd) => Oldingi(a.Id, a.TotalLitr, qayd);
    public static decimal Oldingi(Aparat a, IEnumerable<SmenaKorsatkichDto> qayd) => Oldingi(a.Id, a.TotalLitr, qayd);

    /// <summary>
    /// §8.15: ochiq smenada shu aparatning narx o'zgarishi segmentlarida qayd etilgan litr (hali bakdan ayrilmagan).
    /// Ochiq smenada ogohlantirish: chiqyapti = shu litr + kiritilayotgan qator litri, bor = BakQoldiq (kamaytirilmaydi — manfiy "bor" chiqmaydi).
    /// Tuzatilayotgan segment (istisno) bu yig'indiga kirmaydi — u yangi qiymati bilan qator litri sifatida qo'shiladi.
    /// </summary>
    public static decimal QaydLitri(Aparat a, IEnumerable<SmenaKorsatkichDto> qayd, SmenaKorsatkichDto? istisno = null) =>
        qayd.Where(k => k.AparatId == a.Id && k.NarxOzgarishida && !Equals(k, istisno)).Sum(k => k.Litr);

    /// <summary>§8.15: chiqadigan litr bakdagidan ko'p bo'lsa — sariq ogohlantirish matni, aks holda null (bloklamaydi; bo'sh qator — null).</summary>
    public static string? BakOgohi(decimal? chiqadi, decimal bakdaBor) =>
        chiqadi is { } c && c > 0 && c > bakdaBor ? Til.F("BakOgoh_Matn", Format.Son(bakdaBor), Format.Son(c)) : null;

    /// <summary>Tasdiq dialogidagi qator: "4-aparat: 990 000.00 L (bakda 5 017.83 L)".</summary>
    public static string BakOgohQatori(string nomi, decimal chiqadi, decimal bakdaBor) =>
        Til.F("BakOgoh_Qator", nomi, Format.Son(chiqadi), Format.Son(bakdaBor));

    public static SmenaNatijasi Hisobla(long qaytim, long ochishTerminal, long ochishDepozit, long savdo,
        long qaytganNasiya, long nasiyaJami, long xarajatJami, long yopishTerminal, long yopishDepozit, long sanalganNaqd)
    {
        var plastik = yopishTerminal - ochishTerminal;
        var depozitFarqi = yopishDepozit - ochishDepozit;
        var kutilgan = qaytim + savdo + qaytganNasiya - plastik - depozitFarqi - nasiyaJami - xarajatJami;
        return new SmenaNatijasi(savdo, plastik, depozitFarqi, kutilgan, sanalganNaqd - kutilgan);
    }
}
