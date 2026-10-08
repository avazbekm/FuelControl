namespace FuelControl.Core.Xizmatlar;

/// <summary>Narx o'zgarishida (ochiq smenada) ayrim aparatlarning hozirgi pult ko'rsatkichi berilmagan: kerakli aparat id'lari.</summary>
public sealed class KorsatkichKerakXatosi(int[] kerakliAparatlar)
    : ArgumentException("Narx o'zgarishi uchun ochiq smenadagi aparatlarning hozirgi pult ko'rsatkichi kerak.")
{
    public int[] KerakliAparatlar { get; } = kerakliAparatlar;
}
