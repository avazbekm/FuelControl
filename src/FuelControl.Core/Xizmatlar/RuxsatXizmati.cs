using FuelControl.Contracts;

namespace FuelControl.Core.Xizmatlar;

/// <summary>Rol bo'yicha boshlang'ich ruxsatlar to'plami. Keyin Admin har foydalanuvchiga alohida o'zgartiradi (rolga bog'liq emas).</summary>
public static class RuxsatXizmati
{
    public static readonly Ruxsat[] Hammasi = Enum.GetValues<Ruxsat>();

    public static HashSet<Ruxsat> Standart(Rol rol) => rol switch
    {
        Rol.Operator => new() { Ruxsat.SotuvKiritish, Ruxsat.SmenaOchish, Ruxsat.SmenaYopish },
        Rol.Boshliq => new()
        {
            Ruxsat.Boshqaruv, Ruxsat.SotuvKiritish, Ruxsat.Smenalar, Ruxsat.Hisobotlar, Ruxsat.Operatorlar, Ruxsat.Audit,
            Ruxsat.SmenaOchish, Ruxsat.SmenaYopish, Ruxsat.SotuvTahrirlash, Ruxsat.SotuvBekorQilish, Ruxsat.AvansBerish, Ruxsat.Eksport,
        },
        Rol.Admin => new(Hammasi),
        _ => throw new ArgumentOutOfRangeException(nameof(rol)),
    };
}
