namespace FuelControl.Contracts.Dto;

/// <summary>To'lov turlari bo'yicha taqsimot (Naqd = Savdo - Plastik - Depozit(farqi) - Nasiya).</summary>
public sealed record TolovTaqsimotiDto(long Naqd, long Plastik, long Depozit, long Nasiya);

/// <summary>
/// Boshqaruv paneli. Oy* - joriy (Toshkent) oyda ochilgan yopilgan smenalar bo'yicha; OyKamomat/OyOrtiqcha - musbat sonlar.
/// JoriySmena - ochiq smena (yo'q bo'lsa null); OxirgiYopilgan - oxirgi yopilgan smena.
/// </summary>
public sealed record BoshqaruvDto(SmenaDto? JoriySmena, SmenaDto? OxirgiYopilgan,
    long OySavdo, decimal OyLitr, int OySmenaSoni, long OyKamomat, long OyOrtiqcha,
    NasiyalarXulosaDto Nasiyalar, AparatDto[] Aparatlar,
    SmenaQisqaDto[] OxirgiSmenalar,        // 14 ta yopilgan, eskisidan yangisiga
    TolovTaqsimotiDto OyTolovlar, SmenaDto[] OxirgiYopilganlar);   // 3 ta
