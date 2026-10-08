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

    public static SmenaNatijasi Hisobla(long qaytim, long ochishTerminal, long ochishDepozit, long savdo,
        long qaytganNasiya, long nasiyaJami, long xarajatJami, long yopishTerminal, long yopishDepozit, long sanalganNaqd)
    {
        var plastik = yopishTerminal - ochishTerminal;
        var depozitFarqi = yopishDepozit - ochishDepozit;
        var kutilgan = qaytim + savdo + qaytganNasiya - plastik - depozitFarqi - nasiyaJami - xarajatJami;
        return new SmenaNatijasi(savdo, plastik, depozitFarqi, kutilgan, sanalganNaqd - kutilgan);
    }
}
