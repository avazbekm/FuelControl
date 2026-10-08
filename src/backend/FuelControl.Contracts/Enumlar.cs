namespace FuelControl.Contracts;

public enum Rol { Operator, Boshliq, Admin }

/// <summary>Nasiya qaytishi usuli. (Eski "Click" — hamma joyda "Depozit".) Terminal = Plastik.</summary>
public enum TolovTuri { Naqd, Plastik, Depozit }

public enum XarajatManbai { Kassa, Depozit }

/// <summary>Faol — qarz bor, muddat o'tmagan; MuddatiOtgan — qarz bor, muddat (Toshkent sanasi bo'yicha) o'tgan; Yopilgan — qoldiq 0.</summary>
public enum NasiyaHolati { Faol, MuddatiOtgan, Yopilgan }

public enum HisobotGuruhi { Smena, Kun, Oy, Operator }

public enum HarakatTuri { Maosh, Avans, Kamomat, Ortiqcha, Tolov }

/// <summary>Bo'lim va amallar uchun ruxsatlar. Har foydalanuvchiga alohida beriladi (rol faqat standart to'plamni beradi).</summary>
public enum Ruxsat
{
    Boshqaruv,
    Savdo,
    SmenaOchish,
    SmenaYopish,
    Smenalar,
    Nasiyalar,
    NasiyaYozish,
    QarzQaytdi,
    XarajatYozish,
    BakKirim,
    KorsatkichTuzatish,
    Hisobotlar,
    Eksport,
    Operatorlar,
    AvansBerish,
    Audit,
    Sozlamalar,
}
