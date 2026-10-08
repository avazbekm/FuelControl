namespace FuelControl.Core.Modellar;

public sealed class Aparat
{
    public int Id { get; set; }
    public int Raqam { get; set; }
    public int YoqilgiTuriId { get; set; }

    /// <summary>Pultdagi "Total, L" — oxirgi yopilgan smena holatida: smena yopilganda yangi ko'rsatkichga teng bo'ladi.</summary>
    public decimal TotalLitr { get; set; }

    /// <summary>Aparatning o'z baki, litrda: boshlang'ich qoldiq + kirimlar − sotilgan (smena yopilganda ayriladi). Qo'lda tuzatish faqat Sozlamalarda.</summary>
    public decimal BakQoldiq { get; set; }
}
