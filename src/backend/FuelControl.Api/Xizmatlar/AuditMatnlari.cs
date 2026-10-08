using FuelControl.Contracts;
using FuelControl.Core.Modellar;
using FuelControl.Core.Xizmatlar;

namespace FuelControl.Api.Xizmatlar;

/// <summary>Audit tafsilot matnlari - dizayndagi uslubda: " · " bilan ajratilgan, raqamlar guruhlangan ("1 500 000", "184 642.30").</summary>
public static class AuditMatnlari
{
    public static string FarqMatni(long farq) => farq == 0 ? "farq yo'q" : farq < 0 ? $"kamomat {Format.Pul(-farq)}" : $"ortiqcha {Format.Pul(farq)}";

    public static string UsulMatni(TolovTuri usul) => usul switch
    {
        TolovTuri.Naqd => "naqd",
        TolovTuri.Plastik => "plastik",
        _ => "depozit",
    };

    public static string ManbaMatni(XarajatManbai manba) => manba == XarajatManbai.Kassa ? "kassadan" : "depozitdan";

    /// <summary>#42 · qaytim puli 100 000 · terminal 200 000 · depozit 1 250 000</summary>
    public static string SmenaOchildi(Smena s) =>
        $"#{s.Id} · qaytim puli {Format.Pul(s.OchishQaytim)} · terminal {Format.Pul(s.OchishTerminal)} · depozit {Format.Pul(s.OchishDepozit)}";

    /// <summary>#41 · savdo 15 995 270 · 1 198.40 L · farq yo'q</summary>
    public static string SmenaYopildi(Smena s) =>
        $"#{s.Id} · savdo {Format.Pul(s.Savdo)} · {Format.Litr(s.JamiLitr)} L · {FarqMatni(s.Farq)}";

    /// <summary>Farhod Ismoilov · 30 B 456 CA · 220 000 · 07.10 gacha (mashina raqami bo'lmasa - telefon)</summary>
    public static string NasiyaYozildi(Nasiya n) =>
        $"{n.MijozIsmi} · {(n.MashinaRaqami.Length > 0 ? n.MashinaRaqami : n.Telefon)} · {Format.Pul(n.Summa)} · {Format.KunOy(n.Muddat)} gacha";

    public static string NasiyaOchirildi(Nasiya n) =>
        $"{n.MijozIsmi} · {Format.Pul(n.Summa)} · smena #{n.SmenaId}";

    /// <summary>Bobur Aliyev · 300 000 naqd · qolgan qarz 300 000</summary>
    public static string QarzQaytdi(Nasiya n, NasiyaQaytishi q) =>
        $"{n.MijozIsmi} · {Format.Pul(q.Summa)} {UsulMatni(q.Usul)} · qolgan qarz {Format.Pul(n.Qoldiq)}";

    public static string QaytishOchirildi(Nasiya n, NasiyaQaytishi q) =>
        $"{n.MijozIsmi} · {Format.Pul(q.Summa)} {UsulMatni(q.Usul)} · qolgan qarz {Format.Pul(n.Qoldiq)}";

    /// <summary>Lampochka va tozalash vositasi · 85 000 · kassadan · smena #42</summary>
    public static string Xarajat(Xarajat x) => $"{x.Sabab} · {Format.Pul(x.Summa)} · {ManbaMatni(x.Manba)} · smena #{x.SmenaId}";

    /// <summary>5-aparat, Dizel · +8 000 L · bak 9 480 L bo'ldi · yuk xati 1176 (hujjat bo'lsa)</summary>
    public static string BakKirimi(int aparatRaqami, string yoqilgiNomi, BakKirim k) =>
        $"{aparatRaqami}-aparat, {yoqilgiNomi} · +{Format.LitrIxcham(k.Litr)} L · bak {Format.LitrIxcham(k.QoldiqKeyin)} L bo'ldi" + (k.Hujjat is null ? "" : $" · {k.Hujjat}");

    /// <summary>Smena #40 · 3-aparat yangi ko'rsatkich 62 949.60 dan 62 946.90 ga · sabab: yozishda xato</summary>
    public static string KorsatkichTuzatildi(int smenaId, int aparatRaqami, decimal eski, decimal yangi, string sabab) =>
        $"Smena #{smenaId} · {aparatRaqami}-aparat yangi ko'rsatkich {Format.Litr(eski)} dan {Format.Litr(yangi)} ga · sabab: {sabab.Trim()}";

    public static string NarxOzgardi(string yoqilgi, long eski, long yangi, int segmentSoni) =>
        $"{yoqilgi}: {Format.Pul(eski)} dan {Format.Pul(yangi)} ga" + (segmentSoni > 0 ? $" · ochiq smenada {segmentSoni} ta aparat ko'rsatkichi qayd etildi" : "");

    /// <summary>6-aparat, AI-92 · pult 1 234.50 L · bak 0.00 L</summary>
    public static string AparatYaratildi(Aparat a, string yoqilgiNomi) =>
        $"{a.Raqam}-aparat, {yoqilgiNomi} · pult {Format.Litr(a.TotalLitr)} L · bak {Format.Litr(a.BakQoldiq)} L";

    public static string AparatOzgartirildi(int eskiRaqam, int yangiRaqam, string yoqilgiNomi) =>
        eskiRaqam == yangiRaqam ? $"{yangiRaqam}-aparat · yoqilg'i {yoqilgiNomi}" : $"{eskiRaqam}-aparat raqami {yangiRaqam} ga o'zgardi · yoqilg'i {yoqilgiNomi}";

    public static string BakTuzatildi(int aparatRaqami, decimal eski, decimal yangi, string sabab) =>
        $"{aparatRaqami}-aparat bak qoldig'i {Format.Litr(eski)} dan {Format.Litr(yangi)} ga · sabab: {sabab.Trim()}";

    public static string TotalTuzatildi(int aparatRaqami, decimal eski, decimal yangi, string sabab) =>
        $"{aparatRaqami}-aparat pult ko'rsatkichi {Format.Litr(eski)} dan {Format.Litr(yangi)} ga · sabab: {sabab.Trim()}";

    public static string Avans(string ism, long summa, string? izoh) =>
        $"{ism} · {Format.Pul(summa)}" + (string.IsNullOrWhiteSpace(izoh) ? "" : $" · {izoh.Trim()}");
}
