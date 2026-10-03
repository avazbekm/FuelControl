using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading;

namespace FuelControl.Desktop.Services;

/// <summary>
/// Interfeys tili: uz (lotin), uzk (kirill), ru. XAML'da {l:T Kalit}, kodda Til.T("Kalit").
/// Til almashganda indekser bog'lanishlari ("Item[]") va Ozgardi hodisasi orqali hamma matn yangilanadi.
/// </summary>
public sealed class Til : INotifyPropertyChanged
{
    public static Til Joriy { get; } = new();
    public static event Action? Ozgardi;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Kod { get; private set; } = "uz";
    public bool Lotin => Kod == "uz";
    public bool Kirill => Kod == "uzk";
    public bool Ruscha => Kod == "ru";
    public string Qisqa => Kod switch { "uzk" => "ЎЗ", "ru" => "RU", _ => "UZ" };

    public string this[string kalit] => T(kalit);

    public static string T(string kalit)
    {
        if (!Lugat.TryGetValue(kalit, out var q)) return kalit;
        return Joriy.Kod switch { "uzk" => q.Kir, "ru" => q.Ru, _ => q.Lat };
    }

    public static string F(string kalit, params object[] args) => string.Format(T(kalit), args);

    public void Tanla(string kod)
    {
        if (kod == Kod) return;
        Kod = kod;
        var c = (CultureInfo)CultureInfo.GetCultureInfo(kod switch { "uzk" => "uz-Cyrl-UZ", "ru" => "ru-RU", _ => "uz-Latn-UZ" }).Clone();
        c.NumberFormat.NumberDecimalSeparator = ".";
        c.NumberFormat.NumberGroupSeparator = " ";
        CultureInfo.DefaultThreadCurrentCulture = c;
        CultureInfo.DefaultThreadCurrentUICulture = c;
        Thread.CurrentThread.CurrentCulture = c;
        Thread.CurrentThread.CurrentUICulture = c;
        foreach (var n in new[] { nameof(Kod), nameof(Qisqa), nameof(Lotin), nameof(Kirill), nameof(Ruscha), "Item[]", string.Empty })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        Ozgardi?.Invoke();
    }

    /// <summary>Keyingi tilga o'tish (yig'ilgan menyuda bitta tugma uchun).</summary>
    public void Keyingi() => Tanla(Kod switch { "uz" => "uzk", "uzk" => "ru", _ => "uz" });

    // Kalit → (lotin, kirill, ruscha)
    private static readonly Dictionary<string, (string Lat, string Kir, string Ru)> Lugat = new()
    {
        // ---- Umumiy / menyu
        ["BrendIzoh"] = ("Shoxobcha nazorati", "Шохобча назорати", "Контроль АЗС"),
        ["Boshqaruv"] = ("Boshqaruv", "Бошқарув", "Панель"),
        ["BoshqaruvPaneli"] = ("Boshqaruv paneli", "Бошқарув панели", "Панель управления"),
        ["SotuvKiritish"] = ("Sotuv kiritish", "Сотув киритиш", "Ввод продажи"),
        ["Smenalar"] = ("Smenalar", "Сменалар", "Смены"),
        ["Hisobotlar"] = ("Hisobotlar", "Ҳисоботлар", "Отчёты"),
        ["Operatorlar"] = ("Operatorlar", "Операторлар", "Операторы"),
        ["OperatorlarHisobi"] = ("Operatorlar hisobi", "Операторлар ҳисоби", "Счета операторов"),
        ["AuditJurnali"] = ("Audit jurnali", "Аудит журнали", "Журнал аудита"),
        ["Sozlamalar"] = ("Sozlamalar", "Созламалар", "Настройки"),
        ["MenyuniYig"] = ("Menyuni yig'ish", "Менюни йиғиш", "Свернуть меню"),
        ["MenyuniOch"] = ("Menyuni ochish", "Менюни очиш", "Развернуть меню"),
        ["QorongiRejim"] = ("Qorong'i rejim", "Қоронғи режим", "Тёмная тема"),
        ["RejimIzoh"] = ("Yorug' / qorong'i rejim", "Ёруғ / қоронғи режим", "Светлая / тёмная тема"),
        ["Chiqish"] = ("Chiqish", "Чиқиш", "Выйти"),
        ["ServerAloqa"] = ("Server bilan aloqa bor", "Сервер билан алоқа бор", "Связь с сервером есть"),
        ["ServerAloqaYoq"] = ("Aloqa yo'q", "Алоқа йўқ", "Нет связи"),
        ["Til"] = ("Til", "Тил", "Язык"),
        ["Som"] = ("so'm", "сўм", "сум"),
        ["L"] = ("l", "л", "л"),
        ["Soat"] = ("s", "с", "ч"),
        ["Min"] = ("min", "мин", "мин"),
        ["Jami"] = ("Jami", "Жами", "Итого"),
        ["Smena"] = ("Smena", "Смена", "Смена"),
        ["YoqilgiQoshish"] = ("Yoqilg'i qo'shish", "Ёқилғи қўшиш", "Добавить топливо"),
        ["YoqilginiTahrirlash"] = ("Yoqilg'ini tahrirlash", "Ёқилғини таҳрирлаш", "Изменить топливо"),
        ["YangiYoqilgi"] = ("Yangi yoqilg'i", "Янги ёқилғи", "Новое топливо"),
        ["Nomi"] = ("Nomi", "Номи", "Название"),
        ["Rang"] = ("Rang", "Ранг", "Цвет"),
        ["Ochirish"] = ("O'chirish", "Ўчириш", "Удалить"),
        ["Xato_YoqilgiBand"] = ("Bu nomli yoqilg'i allaqachon bor", "Бу номли ёқилғи аллақачон бор", "Топливо с таким названием уже есть"),
        ["Xato_YoqilgiIshlatilmoqda"] = ("Bu yoqilg'i aparatlarga biriktirilgan — avval aparatlarni o'zgartiring", "Бу ёқилғи апаратларга бириктирилган — аввал апаратларни ўзгартиринг", "Это топливо привязано к колонкам — сначала измените колонки"),
        ["NomiMisol"] = ("Masalan: AI-92, AI-95, Dizel, Metan", "Масалан: AI-92, AI-95, Дизел, Метан", "Например: АИ-92, АИ-95, Дизель, Метан"),
        ["BoshlangichQoldiq"] = ("Boshlang'ich qoldiq", "Бошланғич қолдиқ", "Начальный остаток"),
        ["YakuniyQoldiq"] = ("Yakuniy qoldiq", "Якуний қолдиқ", "Конечный остаток"),
        ["Tolovlar"] = ("To'lovlar", "Тўловлар", "Выплаты"),
        ["ExcelgaChiqarish"] = ("Excel (CSV) ga chiqarish", "Excel (CSV) га чиқариш", "Выгрузить в Excel (CSV)"),
        ["FaylSaqlandi"] = ("Fayl saqlandi", "Файл сақланди", "Файл сохранён"),
        ["Yopish"] = ("Yopish", "Ёпиш", "Закрыть"),
        ["OldingiOy"] = ("Oldingi oy", "Олдинги ой", "Предыдущий месяц"),
        ["KeyingiOy"] = ("Keyingi oy", "Кейинги ой", "Следующий месяц"),
        ["Jami_Kirim"] = ("Jami kirim", "Жами кирим", "Всего начислено"),
        ["Jami_Chiqim"] = ("Jami chiqim", "Жами чиқим", "Всего удержано"),
        ["Totalizator"] = ("Totalizator (pult ko'rsatkichi)", "Тотализатор (пульт кўрсаткичи)", "Тотализатор (показания пульта)"),
        ["TotalizatorIzoh"] = ("Pultdagi Total → L qiymatini kiriting; har sotuvda litr avtomatik qo'shilib boradi", "Пультдаги Total → L қийматини киритинг; ҳар сотувда литр автоматик қўшилиб боради", "Введите значение Total → L с пульта; при каждой продаже литры добавляются автоматически"),
        ["TotalL"] = ("L — jami litr", "L — жами литр", "L — всего литров"),
        ["AparatRaqami"] = ("Aparat raqami", "Апарат рақами", "Номер колонки"),
        ["YoqilgiTuri"] = ("Yoqilg'i turi", "Ёқилғи тури", "Вид топлива"),
        ["Qoshish"] = ("Qo'shish", "Қўшиш", "Добавить"),
        ["YangiAparat"] = ("Yangi aparat", "Янги апарат", "Новая колонка"),
        ["AparatniTahrirlash"] = ("Aparatni tahrirlash", "Апаратни таҳрирлаш", "Изменить колонку"),
        ["YangiFoydalanuvchi"] = ("Yangi foydalanuvchi", "Янги фойдаланувчи", "Новый пользователь"),
        ["FoydalanuvchiniTahrirlash"] = ("Foydalanuvchini tahrirlash", "Фойдаланувчини таҳрирлаш", "Изменить пользователя"),
        ["PinYokiParol"] = ("PIN / parol", "PIN / парол", "PIN / пароль"),
        ["YangiPinParol"] = ("Yangi PIN / parol", "Янги PIN / парол", "Новый PIN / пароль"),
        ["Tiklash"] = ("Tiklash", "Тиклаш", "Сбросить"),
        ["Nofaol"] = ("Nofaol", "Нофаол", "Отключён"),
        ["FaolHolat"] = ("Faol (tizimga kira oladi)", "Фаол (тизимга кира олади)", "Активен (может входить)"),
        ["NusxaOlindi"] = ("Nusxa olindi", "Нусха олинди", "Копия создана"),
        ["Muvaffaqiyatli"] = ("muvaffaqiyatli", "муваффақиятли", "успешно"),
        ["Xato_Maydon"] = ("Barcha maydonlarni to'ldiring", "Барча майдонларни тўлдиринг", "Заполните все поля"),
        ["Xato_AparatBand"] = ("Bu raqamli aparat allaqachon bor", "Бу рақамли апарат аллақачон бор", "Колонка с таким номером уже есть"),
        ["Xato_LoginBand"] = ("Bu login band", "Бу логин банд", "Этот логин занят"),
        ["PinTiklandi"] = ("PIN/parol yangilandi", "PIN/парол янгиланди", "PIN/пароль обновлён"),
        ["ExcelPdf"] = ("Excel", "Excel", "Excel"),
        ["Lotin"] = ("O'zbek (lotin)", "Ўзбек (лотин)", "Узбекский (лат.)"),
        ["Kirill"] = ("O'zbek (kirill)", "Ўзбек (кирилл)", "Узбекский (кир.)"),
        ["Rus"] = ("Ruscha", "Русча", "Русский"),
        ["Vaqt"] = ("Vaqt", "Вақт", "Время"),
        ["Sana"] = ("Sana", "Сана", "Дата"),
        ["Oy"] = ("Oy", "Ой", "Месяц"),
        ["Operator"] = ("Operator", "Оператор", "Оператор"),
        ["Aparat"] = ("Aparat", "Aparat", "Колонка"),
        ["Ap"] = ("Ap.", "Ап.", "Кол."),
        ["Yoqilgi"] = ("Yoqilg'i", "Ёқилғи", "Топливо"),
        ["Litr"] = ("Litr", "Литр", "Литры"),
        ["Summa"] = ("Summa", "Сумма", "Сумма"),
        ["Tolov"] = ("To'lov", "Тўлов", "Оплата"),
        ["Naqd"] = ("Naqd", "Нақд", "Наличные"),
        ["Plastik"] = ("Plastik", "Пластик", "Карта"),
        ["Click"] = ("Click", "Click", "Click"),
        ["Aralash"] = ("Aralash", "Аралаш", "Смешанная"),
        ["BekorQilish"] = ("Bekor qilish", "Бекор қилиш", "Отмена"),
        ["Saqlash"] = ("Saqlash", "Сақлаш", "Сохранить"),
        ["Tahrirlash"] = ("Tahrirlash", "Таҳрирлаш", "Изменить"),
        ["Izoh"] = ("Izoh", "Изоҳ", "Комментарий"),
        ["Kim"] = ("Kim", "Ким", "Кто"),

        // ---- Rollar
        ["Rol_Operator"] = ("Operator", "Оператор", "Оператор"),
        ["Rol_Boshliq"] = ("Boshliq", "Бошлиқ", "Руководитель"),
        ["Rol_Admin"] = ("Admin", "Админ", "Админ"),

        // ---- Kirish
        ["TizimgaKirish"] = ("Tizimga kirish", "Тизимга кириш", "Вход в систему"),
        ["KirishIzoh"] = ("Login va parol (operator — PIN) bilan", "Логин ва парол (оператор — PIN) билан", "Логин и пароль (оператор — PIN)"),
        ["Foydalanuvchi"] = ("Foydalanuvchi", "Фойдаланувчи", "Пользователь"),
        ["PinKod"] = ("PIN kod", "PIN код", "PIN-код"),
        ["Parol"] = ("Parol", "Парол", "Пароль"),
        ["ParolniKiriting"] = ("Parolni kiriting", "Паролни киритинг", "Введите пароль"),
        ["Kirish"] = ("Kirish", "Кириш", "Войти"),
        ["LoginShior"] = ("Shoxobcha savdosi, smenalar va operatorlar — bir ekranda, real vaqtda.", "Шохобча савдоси, сменалар ва операторлар — бир экранда, реал вақтда.", "Продажи АЗС, смены и операторы — на одном экране, в реальном времени."),
        ["SmenaKamomati"] = ("Smena kamomati", "Смена камомати", "Недостача смены"),
        ["OperatorHisobi"] = ("Operator hisobi", "Оператор ҳисоби", "Счёт оператора"),
        ["Soni"] = ("Soni", "Сони", "Кол-во"),
        ["BekorQilingan"] = ("Bekor qilingan", "Бекор қилинган", "Отменено"),
        ["LoginniKiriting"] = ("Loginni kiriting", "Логинни киритинг", "Введите логин"),
        ["OxirgiKirganlar"] = ("Shu kompyuterda kirganlar", "Шу компьютерда кирганлар", "Входили на этом компьютере"),
        ["ServerManzili"] = ("Server manzili", "Сервер манзили", "Адрес сервера"),
        ["ServerIzoh"] = ("Server: {0}", "Сервер: {0}", "Сервер: {0}"),
        ["Yuklanmoqda"] = ("Ulanmoqda…", "Уланмоқда…", "Подключение…"),
        ["Xato_ServerManzil"] = ("Server manzili noto'g'ri (masalan: http://192.168.1.10:5000)", "Сервер манзили нотўғри (масалан: http://192.168.1.10:5000)", "Неверный адрес сервера (например: http://192.168.1.10:5000)"),
        ["AloqaYoq"] = ("Server bilan aloqa yo'q", "Сервер билан алоқа йўқ", "Нет связи с сервером"),
        ["SessiyaTugadi"] = ("Sessiya tugadi — qayta kiring", "Сессия тугади — қайта киринг", "Сессия истекла — войдите снова"),
        ["ServerXatosi"] = ("Server xatosi ({0})", "Сервер хатоси ({0})", "Ошибка сервера ({0})"),
        ["XatoSarlavha"] = ("Amal bajarilmadi", "Амал бажарилмади", "Действие не выполнено"),
        ["Malumot"] = ("Ma'lumot", "Маълумот", "Информация"),
        ["ZaxiraServerda"] = ("Zaxira nusxalar serverda saqlanadi (/data/zaxira, 30 kun). Nusxani olish uchun server administratoriga murojaat qiling.", "Захира нусхалар серверда сақланади (/data/zaxira, 30 кун). Нусхани олиш учун сервер администраторига мурожаат қилинг.", "Резервные копии хранятся на сервере (/data/zaxira, 30 дней). Для получения копии обратитесь к администратору сервера."),
        ["Xato_Foydalanuvchi"] = ("Foydalanuvchini tanlang", "Фойдаланувчини танланг", "Выберите пользователя"),
        ["Xato_Pin"] = ("PIN 4–6 raqamdan iborat bo'lishi kerak", "PIN 4–6 рақамдан иборат бўлиши керак", "PIN должен содержать 4–6 цифр"),

        // ---- Boshqaruv paneli
        ["Yangilash"] = ("Yangilash", "Янгилаш", "Обновить"),
        ["BugungiHisobot"] = ("Bugungi hisobot", "Бугунги ҳисобот", "Отчёт за сегодня"),
        ["BugungiSavdo"] = ("Bugungi savdo", "Бугунги савдо", "Продажи сегодня"),
        ["KechagigaNisbatan"] = ("kechagiga nisbatan", "кечагига нисбатан", "к вчерашнему"),
        ["SotilganYoqilgi"] = ("Sotilgan yoqilg'i", "Сотилган ёқилғи", "Продано топлива"),
        ["TaSotuv"] = ("ta sotuv", "та сотув", "продаж"),
        ["OchiqSmenalar"] = ("Ochiq smenalar", "Очиқ сменалар", "Открытые смены"),
        ["HozirIshlayotgan"] = ("hozir ishlayotgan operatorlar", "ҳозир ишлаётган операторлар", "операторы на смене"),
        ["ShuOyKamomat"] = ("Shu oy kamomat", "Шу ой камомат", "Недостача за месяц"),
        ["OylikSavdo"] = ("oylik savdo", "ойлик савдо", "продажи за месяц"),
        ["TolovTurlariBugun"] = ("To'lov turlari (bugun)", "Тўлов турлари (бугун)", "Виды оплаты (сегодня)"),
        ["YoqilgiTurlariBugun"] = ("Yoqilg'i turlari (bugun)", "Ёқилғи турлари (бугун)", "Виды топлива (сегодня)"),
        ["Oxirgi14Kun"] = ("Oxirgi 14 kun savdosi", "Охирги 14 кун савдоси", "Продажи за 14 дней"),
        ["OperatorlarBugun"] = ("Operatorlar (bugun)", "Операторлар (бугун)", "Операторы (сегодня)"),
        ["OxirgiSotuvlar"] = ("Oxirgi sotuvlar (real vaqt)", "Охирги сотувлар (реал вақт)", "Последние продажи (онлайн)"),
        ["Smenada"] = ("smenada", "сменада", "на смене"),

        // ---- Sotuv kiritish
        ["SmenaOchiq"] = ("smena ochiq", "смена очиқ", "смена открыта"),
        ["SmenaYopiq"] = ("smena yopiq", "смена ёпиқ", "смена закрыта"),
        ["SmenaOchish"] = ("Smena ochish", "Смена очиш", "Открыть смену"),
        ["SmenaniYopish"] = ("Smenani yopish", "Сменани ёпиш", "Закрыть смену"),
        ["AparatniTanlang"] = ("Aparatni tanlang", "Апаратни танланг", "Выберите колонку"),
        ["SummaYokiLitr"] = ("Summa yoki litr", "Сумма ёки литр", "Сумма или литры"),
        ["TezKiritish"] = ("Tez kiritish", "Тез киритиш", "Быстрый ввод"),
        ["Tozalash"] = ("Tozalash", "Тозалаш", "Очистить"),
        ["AvvalAparat"] = ("Avval aparatni tanlang", "Аввал апаратни танланг", "Сначала выберите колонку"),
        ["TolovTuri"] = ("To'lov turi", "Тўлов тури", "Вид оплаты"),
        ["AralashIzoh"] = ("Aralash to'lov: qismlar yig'indisi sotuv summasiga teng bo'lishi shart", "Аралаш тўлов: қисмлар йиғиндиси сотув суммасига тенг бўлиши шарт", "Смешанная оплата: сумма частей должна равняться сумме продажи"),
        ["Qoldiq"] = ("Qoldiq", "Қолдиқ", "Остаток"),
        ["TekshiribSaqlang"] = ("Tekshirib saqlang", "Текшириб сақланг", "Проверьте и сохраните"),
        ["Narx"] = ("Narx", "Нарх", "Цена"),
        ["JamiSumma"] = ("Jami summa", "Жами сумма", "Итого"),
        ["BugungiSotuvlarim"] = ("Bugungi sotuvlarim", "Бугунги сотувларим", "Мои продажи сегодня"),
        ["SomL"] = ("so'm/l", "сўм/л", "сум/л"),
        ["SomLitr"] = ("so'm / litr", "сўм / литр", "сум / литр"),
        ["Saqlandi"] = ("Saqlandi", "Сақланди", "Сохранено"),
        ["SmenaOchilmagan"] = ("Smena ochilmagan — birinchi sotuvda o'zi ochiladi", "Смена очилмаган — биринчи сотувда ўзи очилади", "Смена не открыта — откроется при первой продаже"),
        ["SmenaHolati"] = ("Smena #{0} · {1} dan · {2} ta sotuv · {3}", "Смена #{0} · {1} дан · {2} та сотув · {3}", "Смена #{0} · с {1} · продаж: {2} · {3}"),

        // ---- Smenalar
        ["Bugun"] = ("Bugun", "Бугун", "Сегодня"),
        ["Kun7"] = ("7 kun", "7 кун", "7 дней"),
        ["ShuOy"] = ("Shu oy", "Шу ой", "Этот месяц"),
        ["Hammasi"] = ("Hammasi", "Ҳаммаси", "Все"),
        ["BarchaOperatorlar"] = ("Barcha operatorlar", "Барча операторлар", "Все операторы"),
        ["HozirOchiq"] = ("Hozir ochiq", "Ҳозир очиқ", "Сейчас открыто"),
        ["ShuOySmenalar"] = ("Shu oy smenalar", "Шу ой сменалар", "Смен за месяц"),
        ["Boshlandi"] = ("Boshlandi", "Бошланди", "Начало"),
        ["Davomiylik"] = ("Davomiylik", "Давомийлик", "Длительность"),
        ["Sotuv"] = ("Sotuv", "Сотув", "Продаж"),
        ["Farq"] = ("Farq", "Фарқ", "Разница"),
        ["Ochiq"] = ("Ochiq", "Очиқ", "Открыта"),
        ["OchiqKichik"] = ("● ochiq", "● очиқ", "● открыта"),
        ["Yopilgan"] = ("Yopilgan", "Ёпилган", "Закрыта"),
        ["Kutilgan"] = ("Kutilgan", "Кутилган", "Ожидается"),
        ["Topshirilgan"] = ("Topshirilgan", "Топширилган", "Сдано"),
        ["SmenaSotuvlari"] = ("Smena sotuvlari", "Смена сотувлари", "Продажи смены"),
        ["SmenaYopishSarlavha"] = ("Smena #{0} ni yopish", "Смена #{0} ни ёпиш", "Закрыть смену #{0}"),
        ["SmenaYopishIzoh"] = ("{0} topshirgan summalarni har to'lov turi bo'yicha kiriting", "{0} топширган суммаларни ҳар тўлов тури бўйича киритинг", "Введите суммы, сданные {0}, по каждому виду оплаты"),
        ["IzohIxtiyoriy"] = ("Izoh (ixtiyoriy)", "Изоҳ (ихтиёрий)", "Комментарий (необязательно)"),
        ["IzohMisol"] = ("Masalan: terminal cheki bilan solishtirildi", "Масалан: терминал чеки билан солиштирилди", "Например: сверено с чеком терминала"),
        ["YopishVaSaqlash"] = ("Yopish va saqlash", "Ёпиш ва сақлаш", "Закрыть и сохранить"),
        ["KamomatYozildi"] = ("Kamomat {0} — operator hisobiga qarz sifatida yoziladi", "Камомат {0} — оператор ҳисобига қарз сифатида ёзилади", "Недостача {0} — будет записана как долг оператора"),
        ["OrtiqchaYozildi"] = ("Ortiqcha {0} — operator hisobiga plus yoziladi", "Ортиқча {0} — оператор ҳисобига плюс ёзилади", "Излишек {0} — будет записан в плюс оператору"),
        ["FarqYoq"] = ("Farq yo'q — smena aniq topshirildi", "Фарқ йўқ — смена аниқ топширилди", "Разницы нет — смена сдана точно"),

        // ---- Hisobotlar
        ["Davr"] = ("Davr", "Давр", "Период"),
        ["Dan"] = ("Dan", "Дан", "С"),
        ["Gacha"] = ("Gacha", "Гача", "По"),
        ["Guruhlash"] = ("Guruhlash", "Гуруҳлаш", "Группировка"),
        ["OperatorBoyicha"] = ("Operator bo'yicha", "Оператор бўйича", "По операторам"),
        ["Kunlik"] = ("Kunlik", "Кунлик", "По дням"),
        ["Oylik"] = ("Oylik", "Ойлик", "По месяцам"),
        ["TezDavr"] = ("Tez davr:", "Тез давр:", "Быстрый период:"),
        ["Kecha"] = ("Kecha", "Кеча", "Вчера"),
        ["Oxirgi7Kun"] = ("Oxirgi 7 kun", "Охирги 7 кун", "Последние 7 дней"),
        ["OtganOy"] = ("O'tgan oy", "Ўтган ой", "Прошлый месяц"),
        ["JamiSavdo"] = ("Jami savdo", "Жами савдо", "Всего продаж"),
        ["KamomatAvans"] = ("Kamomat / avans", "Камомат / аванс", "Недостача / аванс"),
        ["Avans"] = ("Avans", "Аванс", "Аванс"),
        ["AvansKichik"] = ("avans", "аванс", "аванс"),
        ["Bekor"] = ("bekor", "бекор", "отменено"),
        ["Sotuvlar"] = ("Sotuvlar", "Сотувлар", "Продажи"),

        // ---- Operatorlar
        ["OperatorlarIzoh"] = ("Har operatorning hisob-varaqasi: oylik maosh, berilgan avanslar, smena kamomatlari va qoldiq", "Ҳар операторнинг ҳисоб-варақаси: ойлик маош, берилган аванслар, смена камоматлари ва қолдиқ", "Лицевой счёт оператора: оклад, авансы, недостачи смен и остаток"),
        ["Maosh"] = ("Maosh", "Маош", "Оклад"),
        ["JoriyQoldiq"] = ("Joriy qoldiq", "Жорий қолдиқ", "Текущий остаток"),
        ["ShuOyKorsatkich"] = ("Shu oy bo'yicha ko'rsatkichlar", "Шу ой бўйича кўрсаткичлар", "Показатели за месяц"),
        ["HisobVaraqa"] = ("Hisob-varaqa", "Ҳисоб-варақа", "Выписка"),
        ["AvansTolov"] = ("Avans / to'lov", "Аванс / тўлов", "Аванс / выплата"),
        ["AvansOldi"] = ("Avans oldi", "Аванс олди", "Получено авансов"),
        ["Kamomat"] = ("Kamomat", "Камомат", "Недостача"),
        ["Ortiqcha"] = ("Ortiqcha", "Ортиқча", "Излишек"),
        ["HisobHarakatlari"] = ("Hisob harakatlari", "Ҳисоб ҳаракатлари", "Движения по счёту"),
        ["Turi"] = ("Turi", "Тури", "Тип"),
        ["KimYozdi"] = ("Kim yozdi", "Ким ёзди", "Кто записал"),
        ["PulBerish"] = ("Pul berish", "Пул бериш", "Выдача денег"),
        ["MaoshTolovi"] = ("Maosh to'lovi", "Маош тўлови", "Выплата оклада"),
        ["SummaSom"] = ("Summa (so'm)", "Сумма (сўм)", "Сумма (сум)"),
        ["IzohMisol2"] = ("Masalan: oilaviy ehtiyoj uchun", "Масалан: оилавий эҳтиёж учун", "Например: на семейные нужды"),
        ["H_Maosh"] = ("Oylik maosh", "Ойлик маош", "Оклад"),
        ["H_Avans"] = ("Avans", "Аванс", "Аванс"),
        ["H_Kamomat"] = ("Kamomat", "Камомат", "Недостача"),
        ["H_Ortiqcha"] = ("Ortiqcha", "Ортиқча", "Излишек"),
        ["H_Tolov"] = ("To'lov", "Тўлов", "Выплата"),
        ["MaoshBerildi"] = ("Maosh berildi", "Маош берилди", "Оклад выплачен"),

        // ---- Sozlamalar
        ["SozlamalarIzoh"] = ("Narxlar, aparatlar, foydalanuvchilar, ruxsatlar va zaxira nusxa", "Нархлар, апаратлар, фойдаланувчилар, рухсатлар ва захира нусха", "Цены, колонки, пользователи, права и резервная копия"),
        ["YoqilgiNarxlari"] = ("Yoqilg'i narxlari", "Ёқилғи нархлари", "Цены на топливо"),
        ["Aparatlar"] = ("Aparatlar", "Апаратлар", "Колонки"),
        ["Foydalanuvchilar"] = ("Foydalanuvchilar", "Фойдаланувчилар", "Пользователи"),
        ["Ruxsatlar"] = ("Ruxsatlar", "Рухсатлар", "Права"),
        ["ZaxiraNusxa"] = ("Zaxira nusxa", "Захира нусха", "Резервная копия"),
        ["NarxIzoh"] = ("Yangi narx shu paytdan boshlab barcha sotuvlarga qo'llanadi; eski sotuvlar o'z narxida qoladi.", "Янги нарх шу пайтдан бошлаб барча сотувларга қўлланади; эски сотувлар ўз нархида қолади.", "Новая цена применяется ко всем продажам с этого момента; старые продажи сохраняют свою цену."),
        ["Joriy"] = ("Joriy", "Жорий", "Текущая"),
        ["YangiNarx"] = ("Yangi narx", "Янги нарх", "Новая цена"),
        ["NarxTarixi"] = ("Narx tarixi", "Нарх тарихи", "История цен"),
        ["Eski"] = ("Eski", "Эски", "Старая"),
        ["Yangi"] = ("Yangi", "Янги", "Новая"),
        ["TaAparat"] = ("ta aparat", "та апарат", "колонок"),
        ["AparatQoshish"] = ("Aparat qo'shish", "Апарат қўшиш", "Добавить колонку"),
        ["AparatlarIzoh"] = ("Har aparat bitta yoqilg'i turiga bog'lanadi. Keyinroq BeiLin pultlari shu aparatlarga ulanadi.", "Ҳар апарат битта ёқилғи турига боғланади. Кейинроқ BeiLin пультлари шу апаратларга уланади.", "Каждая колонка привязана к одному виду топлива. Позже к ним подключатся пульты BeiLin."),
        ["PultUlanmagan"] = ("Pult ulanmagan", "Пульт уланмаган", "Пульт не подключён"),
        ["FoydalanuvchiQoshish"] = ("Foydalanuvchi qo'shish", "Фойдаланувчи қўшиш", "Добавить пользователя"),
        ["ToliqIsm"] = ("To'liq ism", "Тўлиқ исм", "Полное имя"),
        ["Login"] = ("Login", "Логин", "Логин"),
        ["Rol"] = ("Rol", "Рол", "Роль"),
        ["OylikMaosh"] = ("Oylik maosh", "Ойлик маош", "Оклад"),
        ["Holat"] = ("Holat", "Ҳолат", "Статус"),
        ["Faol"] = ("Faol", "Фаол", "Активен"),
        ["PinParolTiklash"] = ("PIN/parolni tiklash", "PIN/паролни тиклаш", "Сбросить PIN/пароль"),
        ["RolBoyichaStandart"] = ("Rol bo'yicha standart", "Рол бўйича стандарт", "По умолчанию для роли"),
        ["RuxsatIzoh"] = ("Belgilangan ruxsat darhol kuchga kiradi: menyu bo'limlari va tugmalar shunga qarab ko'rinadi.", "Белгиланган рухсат дарҳол кучга киради: меню бўлимлари ва тугмалар шунга қараб кўринади.", "Право применяется сразу: разделы меню и кнопки показываются согласно ему."),
        ["Bolimlar"] = ("Bo'limlar", "Бўлимлар", "Разделы"),
        ["Amallar"] = ("Amallar", "Амаллар", "Действия"),
        ["TaRuxsat"] = ("ta ruxsat", "та рухсат", "прав"),
        ["AvtomatikNusxa"] = ("Server kuniga bir marta nusxa oladi", "Сервер кунига бир марта нусха олади", "Сервер делает копию раз в день"),
        ["NusxaIzoh"] = ("Avtomatik, serverda (/data/zaxira); oxirgi 30 kun saqlanadi", "Автоматик, серверда (/data/zaxira); охирги 30 кун сақланади", "Автоматически, на сервере (/data/zaxira); хранятся последние 30 дней"),
        ["OxirgiNusxa"] = ("Oxirgi nusxa", "Охирги нусха", "Последняя копия"),
        ["OxirgiNusxaIzoh"] = ("Shu sessiyada qo'lda nusxa olinmagan", "Шу сессияда қўлда нусха олинмаган", "В этом сеансе копия вручную не создавалась"),
        ["HozirNusxa"] = ("Hozir nusxa olish", "Ҳозир нусха олиш", "Создать копию сейчас"),
        ["NusxadanTiklash"] = ("Nusxadan tiklash…", "Нусхадан тиклаш…", "Восстановить из копии…"),
        ["PapkaniOchish"] = ("Papkani ochish", "Папкани очиш", "Открыть папку"),

        // Ruxsat nomlari va izohlari
        ["R_Boshqaruv"] = ("Boshqaruv paneli", "Бошқарув панели", "Панель управления"),
        ["RI_Boshqaruv"] = ("Bugungi savdo, KPI va grafiklar", "Бугунги савдо, KPI ва графиклар", "Продажи за день, KPI и графики"),
        ["R_SotuvKiritish"] = ("Sotuv kiritish", "Сотув киритиш", "Ввод продажи"),
        ["RI_SotuvKiritish"] = ("Aparat, summa/litr, to'lov turi bilan sotuv yozish", "Апарат, сумма/литр, тўлов тури билан сотув ёзиш", "Запись продажи: колонка, сумма/литры, вид оплаты"),
        ["R_Smenalar"] = ("Smenalar", "Сменалар", "Смены"),
        ["RI_Smenalar"] = ("Smenalar ro'yxati va tafsiloti", "Сменалар рўйхати ва тафсилоти", "Список смен и детали"),
        ["R_Hisobotlar"] = ("Hisobotlar", "Ҳисоботлар", "Отчёты"),
        ["RI_Hisobotlar"] = ("Davr, operator va yoqilg'i bo'yicha hisobot", "Давр, оператор ва ёқилғи бўйича ҳисобот", "Отчёт по периоду, оператору и топливу"),
        ["R_Operatorlar"] = ("Operatorlar hisobi", "Операторлар ҳисоби", "Счета операторов"),
        ["RI_Operatorlar"] = ("Maosh, avans, kamomat va qoldiq", "Маош, аванс, камомат ва қолдиқ", "Оклад, аванс, недостача и остаток"),
        ["R_Audit"] = ("Audit jurnali", "Аудит журнали", "Журнал аудита"),
        ["RI_Audit"] = ("Kim, qachon, nima qildi", "Ким, қачон, нима қилди", "Кто, когда, что сделал"),
        ["R_Sozlamalar"] = ("Sozlamalar", "Созламалар", "Настройки"),
        ["RI_Sozlamalar"] = ("Narxlar, aparatlar, foydalanuvchilar, ruxsatlar", "Нархлар, апаратлар, фойдаланувчилар, рухсатлар", "Цены, колонки, пользователи, права"),
        ["R_SmenaOchish"] = ("Smena ochish", "Смена очиш", "Открыть смену"),
        ["RI_SmenaOchish"] = ("O'z smenasini ochish (yoki birinchi sotuvda avtomatik)", "Ўз сменасини очиш (ёки биринчи сотувда автоматик)", "Открыть свою смену (или автоматически при первой продаже)"),
        ["R_SmenaYopish"] = ("Smenani yopish", "Сменани ёпиш", "Закрыть смену"),
        ["RI_SmenaYopish"] = ("Topshirilgan naqd/plastik/click kiritib yopish", "Топширилган нақд/пластик/click киритиб ёпиш", "Закрыть, указав сданные наличные/карту/Click"),
        ["R_SotuvTahrirlash"] = ("Sotuvni tahrirlash", "Сотувни таҳрирлаш", "Изменение продажи"),
        ["RI_SotuvTahrirlash"] = ("Xato kiritilgan sotuvni sabab yozib tuzatish", "Хато киритилган сотувни сабаб ёзиб тузатиш", "Исправление ошибочной продажи с указанием причины"),
        ["SotuvniTahrirlashSarlavha"] = ("Sotuvni tahrirlash #{0}", "Сотувни таҳрирлаш #{0}", "Изменить продажу #{0}"),
        ["SababMajbur"] = ("Sabab yozilishi shart — audit jurnaliga tushadi", "Сабаб ёзилиши шарт — аудит журналига тушади", "Укажите причину — она попадёт в журнал аудита"),
        ["Hozirgi"] = ("Hozirgi", "Ҳозирги", "Текущее"),
        ["SotuvniBekorQilish"] = ("Sotuvni bekor qilish", "Сотувни бекор қилиш", "Отменить продажу"),
        ["SababMisol"] = ("Masalan: operator summani xato kiritgan", "Масалан: оператор суммани хато киритган", "Например: оператор ввёл неверную сумму"),
        ["R_SotuvBekorQilish"] = ("Sotuvni bekor qilish", "Сотувни бекор қилиш", "Отмена продажи"),
        ["RI_SotuvBekorQilish"] = ("Sabab yozib bekor qilish (o'chirilmaydi)", "Сабаб ёзиб бекор қилиш (ўчирилмайди)", "Отмена с указанием причины (не удаляется)"),
        ["R_AvansBerish"] = ("Avans / to'lov berish", "Аванс / тўлов бериш", "Выдача аванса / выплата"),
        ["RI_AvansBerish"] = ("Operator hisobiga pul yozish", "Оператор ҳисобига пул ёзиш", "Запись денег на счёт оператора"),
        ["R_Eksport"] = ("Excel / PDF eksport", "Excel / PDF экспорт", "Экспорт в Excel / PDF"),
        ["RI_Eksport"] = ("Hisobotni faylga chiqarish", "Ҳисоботни файлга чиқариш", "Выгрузка отчёта в файл"),

        // ---- Audit
        ["AuditIzoh"] = ("Kim, qachon, nima qildi: bekor qilishlar, narx, smena va rol o'zgarishlari", "Ким, қачон, нима қилди: бекор қилишлар, нарх, смена ва рол ўзгаришлари", "Кто, когда, что сделал: отмены, цены, смены и роли"),
        ["Qidirish"] = ("Qidirish…", "Қидириш…", "Поиск…"),
        ["BarchaAmallar"] = ("Barcha amallar", "Барча амаллар", "Все действия"),
        ["BekorQilinganSotuvlar"] = ("Bekor qilingan sotuvlar", "Бекор қилинган сотувлар", "Отменённые продажи"),
        ["Amal"] = ("Amal", "Амал", "Действие"),
        ["Tafsilot"] = ("Tafsilot", "Тафсилот", "Детали"),
        ["Sabab"] = ("Sabab", "Сабаб", "Причина"),
        ["BekorQilgan"] = ("Bekor qilgan", "Бекор қилган", "Отменил"),
    };
}
