namespace FuelControl.Contracts;

public enum Rol { Operator, Boshliq, Admin }

public enum TolovTuri { Naqd, Plastik, Click }

public enum SotuvHolati { Faol, BekorQilingan }

public enum HarakatTuri { Maosh, Avans, Kamomat, Ortiqcha, Tolov }

/// <summary>Bo'lim va amallar uchun ruxsatlar. Har foydalanuvchiga alohida beriladi (rol faqat standart to'plamni beradi).</summary>
public enum Ruxsat
{
    Boshqaruv,
    SotuvKiritish,
    SmenaOchish,
    SmenaYopish,
    Smenalar,
    Hisobotlar,
    Eksport,
    Operatorlar,
    AvansBerish,
    SotuvBekorQilish,
    SotuvTahrirlash,
    Audit,
    Sozlamalar,
}

public enum HisobotGuruhi { Operator, Kun, Oy }
