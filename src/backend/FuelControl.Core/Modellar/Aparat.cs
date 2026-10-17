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

    /// <summary>
    /// Pult 0 ga qaytadigan hajm, litrda (masalan 10 000); null — krug yo'q. Faqat ko'rsatish va kiritish qoidasi: TotalLitr, segmentlar va
    /// formulalar doim umumiy litrda qoladi.
    /// </summary>
    public decimal? KrugHajmi { get; set; }
}
