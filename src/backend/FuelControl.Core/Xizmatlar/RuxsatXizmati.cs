using FuelControl.Contracts;

namespace FuelControl.Core.Xizmatlar;

/// <summary>Rol bo'yicha boshlang'ich ruxsatlar to'plami. Keyin Admin har foydalanuvchiga alohida o'zgartiradi (rolga bog'liq emas).</summary>
public static class RuxsatXizmati
{
    public static readonly Ruxsat[] Hammasi = Enum.GetValues<Ruxsat>();

    public static HashSet<Ruxsat> Standart(Rol rol) => rol switch
    {
        Rol.Operator => new()
        {
            Ruxsat.Savdo, Ruxsat.Nasiyalar, Ruxsat.SmenaOchish, Ruxsat.SmenaYopish,
            Ruxsat.NasiyaYozish, Ruxsat.QarzQaytdi, Ruxsat.XarajatYozish,
        },
        // Boshliq: hammasi, Sozlamalar'dan tashqari.
        Rol.Boshliq => new(Hammasi.Where(r => r != Ruxsat.Sozlamalar)),
        Rol.Admin => new(Hammasi),
        _ => throw new ArgumentOutOfRangeException(nameof(rol)),
    };
}
