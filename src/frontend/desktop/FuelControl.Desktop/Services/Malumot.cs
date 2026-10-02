using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia.Threading;
using FuelControl.Contracts.Dto;
using FuelControl.Desktop.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace FuelControl.Desktop.Services;

[Flags]
public enum Bolim
{
    Foydalanuvchilar = 1,
    Yoqilgilar = 2,
    Aparatlar = 4,
    Smenalar = 8,
    Sotuvlar = 16,
    Harakatlar = 32,
    Audit = 64,
    Hammasi = 127,
}

/// <summary>
/// Serverdagi ma'lumotning lokal keshi. Ro'yxatlar login'dan keyin API'dan yuklanadi va SignalR (/hub) xabarlari
/// kelganda tegishli bo'lim qayta yuklanadi. Yozuvchi amallar faqat API orqali — javobdan kesh yangilanadi.
/// Obyektlar Id bo'yicha joyida yangilanadi, shuning uchun ViewModel'lardagi tanlovlar (aparat, smena) saqlanib qoladi.
/// Hamma o'zgarishlar UI oqimida bajariladi.
/// </summary>
public static class Malumot
{
    public static Foydalanuvchi JoriyFoydalanuvchi { get; private set; } = new();
    public static readonly List<Foydalanuvchi> Foydalanuvchilar = new();
    public static readonly List<YoqilgiTuri> Yoqilgilar = new();
    public static readonly List<Aparat> Aparatlar = new();
    public static readonly List<Smena> Smenalar = new();
    public static readonly List<Sotuv> Sotuvlar = new();
    public static readonly List<HisobHarakati> Harakatlar = new();
    public static readonly List<AuditYozuvi> Audit = new();
    public static readonly List<NarxTarixi> NarxTarixi = new();

    // Keshda faqat yaqin davr: bugungi sotuvlar, oxirgi 60 kun smenalari va bekor qilinganlar, oxirgi 500 audit.
    // Boshqaruv, hisobot, eski smena sotuvlari va "Hammasi" filtri serverdan alohida so'raladi.
    public const int SmenaKunlari = 60;
    public const int AuditSoni = 500;

    /// <summary>Oxirgi 60 kundagi bekor qilingan sotuvlar (Audit sahifasi uchun).</summary>
    public static readonly List<Sotuv> BekorQilinganlar = new();

    /// <summary>Operatorning joriy oy savdosi va smenalar soni (operator hisobi so'rovidan).</summary>
    public static readonly Dictionary<int, (long Savdo, int Smenalar)> OyStatistikasi = new();

    public static bool Kirilgan => _kirilgan;
    public static DateOnly Bugun => DateOnly.FromDateTime(DateTime.Today);

    public static IEnumerable<Foydalanuvchi> Operatorlar => Foydalanuvchilar.Where(f => f.Rol == Rol.Operator);

    /// <summary>Kesh o'zgardi — sahifalar qayta hisoblanadi.</summary>
    public static event Action? Ozgardi;
    public static void OzgardiXabar() => Ozgardi?.Invoke();

    public static ApiMijoz Api { get; private set; } = YangiMijoz(Sozlama.Joriy.ServerManzili);

    /// <summary>SignalR ulanishi bor-yo'qligi. Yo'q bo'lsa yozuvchi tugmalar o'chiriladi (1-versiyada navbat yo'q).</summary>
    public static bool AloqaBor { get; private set; }
    public static event Action? AloqaOzgardi;

    /// <summary>Sessiya tugadi yoki o'z ruxsatlari o'zgardi — qayta kirish kerak. Parametr — sabab matni.</summary>
    public static event Action<string>? MajburiyChiqish;

    private static HubConnection? _hub;
    private static bool _kirilgan;
    private static Bolim _kutilayotgan;
    private static DispatcherTimer? _taymer;

    // Ro'yxatda yo'q, lekin sotuv/smenada uchraydigan foydalanuvchilar (masalan, sotuv qilgan boshliq).
    private static readonly Dictionary<int, Foydalanuvchi> Begonalar = new();

    private static ApiMijoz YangiMijoz(string manzil)
    {
        var m = new ApiMijoz(manzil);
        m.SessiyaTugadi += () => Dispatcher.UIThread.Post(() => MajburiyChiqish?.Invoke(Til.T("SessiyaTugadi")));
        return m;
    }

    // ================= Kirish / chiqish =================

    public static async Task<Foydalanuvchi> Kirish(string manzil, string login, string parol)
    {
        manzil = manzil.Trim().TrimEnd('/');
        if (!Uri.TryCreate(manzil, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            throw new ApiXatosi(Til.T("Xato_ServerManzil"), -1);
        if (Api.Manzil != manzil) Api = YangiMijoz(manzil);

        var javob = await Api.Kirish(login.Trim(), parol);
        Tozala();
        JoriyFoydalanuvchi = Yarat(javob.Foydalanuvchi);
        _kirilgan = true;
        try
        {
            await Yukla(Bolim.Hammasi);
        }
        catch
        {
            await Chiqish();
            throw;
        }
        _ = HubniBoshla();
        return JoriyFoydalanuvchi;
    }

    public static async Task Chiqish()
    {
        _kirilgan = false;
        _taymer?.Stop();
        _kutilayotgan = 0;
        var hub = _hub;
        _hub = null;
        if (hub is not null)
        {
            try { await hub.DisposeAsync(); } catch (Exception) { /* yopilayotgan ulanish */ }
        }
        Api.Chiqish();
        AloqaniOrnat(false);
        Tozala();
        OzgardiXabar();
    }

    private static void Tozala()
    {
        Foydalanuvchilar.Clear(); Yoqilgilar.Clear(); Aparatlar.Clear(); Smenalar.Clear(); Sotuvlar.Clear();
        Harakatlar.Clear(); Audit.Clear(); NarxTarixi.Clear(); Begonalar.Clear(); BekorQilinganlar.Clear(); OyStatistikasi.Clear();
        JoriyFoydalanuvchi = new Foydalanuvchi();
    }

    // ================= Yuklash =================

    /// <summary>Ko'rsatilgan bo'limlarni serverdan qayta yuklaydi (ruxsatga qarab — server baribir o'zinikini qaytaradi).</summary>
    public static async Task Yukla(Bolim b)
    {
        var j = JoriyFoydalanuvchi;

        if (b.HasFlag(Bolim.Foydalanuvchilar))
        {
            // Server ruxsatni har so'rovda bazadan tekshiradi — rol/ruxsat o'zgarsa joyida yangilaymiz (menyu Ozgardi orqali qayta quriladi).
            // Nofaol qilingan foydalanuvchi /me da 401 oladi → SessiyaTugadi → chiqish.
            var men = (await Api.Men())!;
            if (!men.Faol)
            {
                MajburiyChiqish?.Invoke(Til.T("SessiyaTugadi"));
                return;
            }
            j.ToliqIsm = men.ToliqIsm; j.Login = men.Login; j.OylikMaosh = men.OylikMaosh;
            j.Rol = men.Rol;
            if (!j.Ruxsatlar.SetEquals(men.Ruxsatlar)) j.Ruxsatlar = men.Ruxsatlar.ToHashSet();
            var royxat = j.Bor(Ruxsat.Sozlamalar) ? await Api.Foydalanuvchilar() : await Api.Operatorlar();
            Mosla(Foydalanuvchilar, royxat!, d => d.Id, FoydalanuvchiOl, (f, d) =>
            {
                f.ToliqIsm = d.ToliqIsm; f.Login = d.Login; f.Rol = d.Rol; f.Faol = d.Faol; f.OylikMaosh = d.OylikMaosh;
                if (f != j) f.Ruxsatlar = d.Ruxsatlar.ToHashSet();
            });
        }

        if (b.HasFlag(Bolim.Yoqilgilar))
        {
            Mosla(Yoqilgilar, (await Api.Yoqilgilar())!, d => d.Id, d => new YoqilgiTuri { Id = d.Id },
                (y, d) => { y.Nomi = d.Nomi; y.Narx = d.Narx; y.Rang = d.Rang; });
            var tarix = (await Api.NarxTarixi())!;
            NarxTarixi.Clear();
            NarxTarixi.AddRange(tarix.Select(n => new NarxTarixi
            {
                Vaqt = n.Vaqt.ToLocalTime(), Yoqilgi = n.Yoqilgi, EskiNarx = n.EskiNarx, YangiNarx = n.YangiNarx, Kim = n.Kim,
            }));
        }

        if (b.HasFlag(Bolim.Aparatlar))
            Mosla(Aparatlar, (await Api.Aparatlar())!, d => d.Id, d => new Aparat { Id = d.Id }, (a, d) =>
            {
                a.Raqam = d.Raqam;
                a.Yoqilgi = YoqilgiOl(d.YoqilgiTuriId, d.YoqilgiNomi);
                a.TotalLitr = d.TotalLitr;
            });

        if (b.HasFlag(Bolim.Smenalar))
        {
            Mosla(Smenalar, (await Api.Smenalar(Bugun.AddDays(-SmenaKunlari)))!, d => d.Id, SmenaYarat, SmenaYangila);
            Smenalar.Sort((x, y) => y.Boshlandi.CompareTo(x.Boshlandi));
        }

        if (b.HasFlag(Bolim.Sotuvlar))
        {
            Mosla(Sotuvlar, (await Api.Sotuvlar(Bugun, Bugun))!, d => d.Id, SotuvYarat, SotuvYangila);
            Sotuvlar.Sort((x, y) => y.Vaqt.CompareTo(x.Vaqt));
            if (j.Bor(Ruxsat.Audit))
            {
                var bekor = (await Api.Sotuvlar(Bugun.AddDays(-SmenaKunlari), holati: SotuvHolati.BekorQilingan))!;
                BekorQilinganlar.Clear();
                BekorQilinganlar.AddRange(bekor.Select(SotuvKorinishi).OrderByDescending(s => s.Vaqt));
            }
        }

        if (b.HasFlag(Bolim.Harakatlar))
        {
            // Operatorlar ruxsati bo'lsa — barcha operatorlar, aks holda faqat o'zi.
            var idlar = j.Bor(Ruxsat.Operatorlar) ? Operatorlar.Select(o => o.Id).ToList() : [j.Id];
            var yangi = new List<HisobHarakati>();
            foreach (var id in idlar)
            {
                var hisob = (await Api.OperatorHisobi(id))!;
                var op = FoydalanuvchiOl(id, hisob.OperatorIsmi);
                OyStatistikasi[id] = (hisob.OySavdo, hisob.OySmenalar);
                yangi.AddRange(hisob.Harakatlar.Select(h => new HisobHarakati
                {
                    Id = h.Id, Operator = op, Sana = h.Sana.ToLocalTime(), Turi = h.Turi, Summa = h.Summa, Izoh = h.Izoh, KimYozdi = h.KimYozdi,
                }));
            }
            Harakatlar.Clear();
            Harakatlar.AddRange(yangi.OrderByDescending(h => h.Sana).ThenByDescending(h => h.Id));
        }

        if (b.HasFlag(Bolim.Audit) && j.Bor(Ruxsat.Audit))
        {
            var audit = (await Api.Audit(AuditSoni))!;
            Audit.Clear();
            Audit.AddRange(audit.Select(a => new AuditYozuvi { Id = a.Id, Vaqt = a.Vaqt.ToLocalTime(), Kim = a.Kim, Amal = a.Amal, Tafsilot = a.Tafsilot }));
        }

        OzgardiXabar();
    }

    /// <summary>Yozuvchi amaldan keyin qo'shimcha yangilash — xato bo'lsa jim (keyingi SignalR xabari yoki qayta ulanish to'g'rilaydi).</summary>
    private static async Task JimYukla(Bolim b)
    {
        try { await Yukla(b); }
        catch (ApiXatosi) { }
    }

    /// <summary>Bir nechta xabar ketma-ket kelsa, bitta yuklashga birlashtiriladi.</summary>
    private static void Rejala(Bolim b)
    {
        if (!_kirilgan) return;
        _kutilayotgan |= b;
        if (_taymer is null)
        {
            _taymer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _taymer.Tick += async (_, _) =>
            {
                _taymer!.Stop();
                var x = _kutilayotgan;
                _kutilayotgan = 0;
                if (_kirilgan && x != 0) await JimYukla(x);
            };
        }
        _taymer.Stop();
        _taymer.Start();
    }

    // ================= SignalR =================

    private sealed class DoimiyQaytaUlanish : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext c) => c.PreviousRetryCount switch
        {
            0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(5),
            _ => TimeSpan.FromSeconds(10),
        };
    }

    private static async Task HubniBoshla()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(Api.Manzil + "/hub", o => o.AccessTokenProvider = () => Task.FromResult(Api.Token))
            .WithAutomaticReconnect(new DoimiyQaytaUlanish())
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();
        _hub = hub;

        void UI(Action a) => Dispatcher.UIThread.Post(() => { if (_hub == hub) a(); });

        hub.On<SotuvDto>("SotuvQoshildi", d => UI(() => { SotuvniQoy(d); Rejala(Bolim.Aparatlar | Bolim.Smenalar | Bolim.Audit); }));
        hub.On<SotuvDto>("SotuvOzgardi", d => UI(() => { SotuvniQoy(d); Rejala(Bolim.Aparatlar | Bolim.Smenalar | Bolim.Sotuvlar | Bolim.Harakatlar | Bolim.Audit); }));
        hub.On<SmenaDto>("SmenaOzgardi", d => UI(() => { SmenaniQoy(d); Rejala(Bolim.Harakatlar | Bolim.Audit); }));
        hub.On<YoqilgiTuriDto>("NarxOzgardi", _ => UI(() => Rejala(Bolim.Yoqilgilar | Bolim.Aparatlar | Bolim.Audit)));
        hub.On<string>("Ozgardi", bolim => UI(() => Rejala(Bolim.Audit | bolim switch
        {
            "Foydalanuvchilar" => Bolim.Foydalanuvchilar,
            "Yoqilgilar" => Bolim.Yoqilgilar | Bolim.Aparatlar,
            "Aparatlar" => Bolim.Aparatlar,
            "Harakatlar" => Bolim.Harakatlar,
            _ => Bolim.Hammasi,
        })));

        // Server o'zgargan foydalanuvchining ulanishini uzishdan oldin yuboradi: o'z holatimizni darhol tekshiramiz
        // (nofaol bo'lsak /me → 401 → chiqish; ruxsat o'zgargan bo'lsa — joyida yangilanadi, qayta ulanish yangi guruhlar bilan).
        hub.On("QaytaUlan", () => UI(() => Rejala(Bolim.Foydalanuvchilar)));
        hub.Reconnecting += _ => { UI(() => AloqaniOrnat(false)); return Task.CompletedTask; };
        // Uzilish paytida o'tkazib yuborilgan xabarlar — hammasini qayta yuklaymiz.
        hub.Reconnected += _ => { UI(() => { AloqaniOrnat(true); Rejala(Bolim.Hammasi); }); return Task.CompletedTask; };
        hub.Closed += xato => { UI(() => { AloqaniOrnat(false); _ = Ulan(hub, qaytaYukla: true); }); return Task.CompletedTask; };

        await Ulan(hub, qaytaYukla: false);
    }

    /// <summary>Birinchi ulanish (yoki avtomatik qayta ulanish taslim bo'lgach) — muvaffaqiyatgacha har 5 soniyada urinadi.</summary>
    private static async Task Ulan(HubConnection hub, bool qaytaYukla)
    {
        while (_kirilgan && _hub == hub && hub.State == HubConnectionState.Disconnected)
        {
            try
            {
                await hub.StartAsync();
                AloqaniOrnat(true);
                if (qaytaYukla) Rejala(Bolim.Hammasi);
                return;
            }
            catch (Exception)
            {
                AloqaniOrnat(false);
                qaytaYukla = true;
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }

    private static void AloqaniOrnat(bool bor)
    {
        if (AloqaBor == bor) return;
        AloqaBor = bor;
        AloqaOzgardi?.Invoke();
    }

    // ================= Yozuvchi amallar (API → kesh) =================

    public static async Task<Sotuv> SotuvYarat(SotuvYaratishDto d)
    {
        var s = SotuvniQoy((await Api.SotuvYarat(d))!);
        await JimYukla(Bolim.Aparatlar | Bolim.Smenalar | Bolim.Audit);
        return s;
    }

    public static async Task<Sotuv> SotuvTahrirla(int id, SotuvTahrirlashDto d)
    {
        var s = SotuvniQoy((await Api.SotuvTahrirla(id, d))!);
        await JimYukla(Bolim.Aparatlar | Bolim.Smenalar | Bolim.Harakatlar | Bolim.Audit);
        return s;
    }

    public static async Task<Sotuv> SotuvBekorQil(int id, string sabab)
    {
        var s = SotuvniQoy((await Api.SotuvBekorQil(id, new SotuvBekorQilishDto(sabab)))!);
        await JimYukla(Bolim.Aparatlar | Bolim.Smenalar | Bolim.Sotuvlar | Bolim.Harakatlar | Bolim.Audit);
        return s;
    }

    public static async Task<Smena> SmenaOch()
    {
        var s = SmenaniQoy((await Api.SmenaOch())!);
        await JimYukla(Bolim.Audit);
        return s;
    }

    public static async Task<Smena> SmenaYop(int id, SmenaYopishDto d)
    {
        var s = SmenaniQoy((await Api.SmenaYop(id, d))!);
        await JimYukla(Bolim.Harakatlar | Bolim.Audit);
        return s;
    }

    public static async Task HarakatYoz(int operatorId, HarakatYaratishDto d)
    {
        await Api.HarakatYoz(operatorId, d);
        await JimYukla(Bolim.Harakatlar | Bolim.Audit);
    }

    public static async Task YoqilgiYarat(YoqilgiYaratishDto d) { await Api.YoqilgiYarat(d); await JimYukla(Bolim.Yoqilgilar | Bolim.Audit); }
    public static async Task YoqilgiTahrirla(int id, YoqilgiTahrirlashDto d) { await Api.YoqilgiTahrirla(id, d); await JimYukla(Bolim.Yoqilgilar | Bolim.Aparatlar | Bolim.Audit); }
    public static async Task YoqilgiOchir(int id) { await Api.YoqilgiOchir(id); await JimYukla(Bolim.Yoqilgilar | Bolim.Audit); }

    public static async Task AparatYarat(AparatYaratishDto d) { await Api.AparatYarat(d); await JimYukla(Bolim.Aparatlar | Bolim.Audit); }
    public static async Task AparatTahrirla(int id, AparatTahrirlashDto d) { await Api.AparatTahrirla(id, d); await JimYukla(Bolim.Aparatlar | Bolim.Audit); }

    public static async Task<Foydalanuvchi> FoydalanuvchiYarat(FoydalanuvchiYaratishDto d)
    {
        var f = (await Api.FoydalanuvchiYarat(d))!;
        await JimYukla(Bolim.Foydalanuvchilar | Bolim.Audit);
        return FoydalanuvchiOl(f);
    }

    public static async Task FoydalanuvchiTahrirla(int id, FoydalanuvchiTahrirlashDto d, string? yangiPin)
    {
        await Api.FoydalanuvchiTahrirla(id, d);
        if (!string.IsNullOrWhiteSpace(yangiPin)) await Api.PinOrnat(id, new PinOrnatishDto(yangiPin.Trim()));
        await JimYukla(Bolim.Foydalanuvchilar | Bolim.Audit);
    }

    public static async Task RuxsatlarniOrnat(int id, IEnumerable<Ruxsat> ruxsatlar)
    {
        await Api.RuxsatlarniOrnat(id, new RuxsatlarOrnatishDto(ruxsatlar.ToArray()));
        await JimYukla(Bolim.Foydalanuvchilar | Bolim.Audit);
    }

    public static async Task PinOrnat(int id, string pin)
    {
        await Api.PinOrnat(id, new PinOrnatishDto(pin));
        await JimYukla(Bolim.Audit);
    }

    public static async Task<ZaxiraJavobiDto> Zaxira()
    {
        var z = (await Api.Zaxira())!;
        await JimYukla(Bolim.Audit);
        return z;
    }

    // ================= DTO → model =================

    /// <summary>Ro'yxatni server javobiga moslaydi: borini joyida yangilaydi, yangisini qo'shadi, yo'qini olib tashlaydi.</summary>
    private static void Mosla<T, TDto>(List<T> royxat, List<TDto> dtolar, Func<TDto, int> id, Func<TDto, T> yarat, Action<T, TDto> yangila)
        where T : class
    {
        var eskilar = royxat.ToDictionary(x => ModelId(x));
        royxat.Clear();
        foreach (var d in dtolar)
        {
            var x = eskilar.TryGetValue(id(d), out var e) ? e : yarat(d);
            yangila(x, d);
            royxat.Add(x);
        }
    }

    private static int ModelId(object x) => x switch
    {
        Foydalanuvchi f => f.Id, YoqilgiTuri y => y.Id, Aparat a => a.Id, Smena s => s.Id, Sotuv s => s.Id,
        _ => throw new ArgumentException(x.GetType().Name),
    };

    private static Foydalanuvchi Yarat(FoydalanuvchiDto d) => new()
    {
        Id = d.Id, ToliqIsm = d.ToliqIsm, Login = d.Login, Rol = d.Rol, Faol = d.Faol, OylikMaosh = d.OylikMaosh,
        Ruxsatlar = d.Ruxsatlar.ToHashSet(),
    };

    private static Foydalanuvchi FoydalanuvchiOl(FoydalanuvchiDto d) => FoydalanuvchiOl(d.Id, d.ToliqIsm);

    private static Foydalanuvchi FoydalanuvchiOl(int id, string ism)
    {
        if (JoriyFoydalanuvchi.Id == id) return JoriyFoydalanuvchi;
        var f = Foydalanuvchilar.FirstOrDefault(x => x.Id == id);
        if (f is not null) return f;
        if (!Begonalar.TryGetValue(id, out f))
            Begonalar[id] = f = new Foydalanuvchi { Id = id, ToliqIsm = ism, Rol = Rol.Boshliq, Faol = false };
        return f;
    }

    private static YoqilgiTuri YoqilgiOl(int id, string nomi) =>
        Yoqilgilar.FirstOrDefault(y => y.Id == id) ?? new YoqilgiTuri { Id = id, Nomi = nomi };

    private static Aparat AparatOl(int id, int raqam, string yoqilgi) =>
        Aparatlar.FirstOrDefault(a => a.Id == id)
        ?? new Aparat { Id = id, Raqam = raqam, Yoqilgi = Yoqilgilar.FirstOrDefault(y => y.Nomi == yoqilgi) ?? new YoqilgiTuri { Nomi = yoqilgi } };

    private static Smena SmenaYarat(SmenaDto d) =>
        new() { Id = d.Id, Operator = FoydalanuvchiOl(d.OperatorId, d.OperatorIsmi), Boshlandi = d.Boshlandi.ToLocalTime() };

    private static void SmenaYangila(Smena s, SmenaDto d)
    {
        s.Tugadi = d.Tugadi?.ToLocalTime();
        s.KutilganNaqd = d.KutilganNaqd; s.KutilganPlastik = d.KutilganPlastik; s.KutilganClick = d.KutilganClick;
        s.JamiLitr = d.JamiLitr; s.SotuvSoni = d.SotuvSoni;
        s.TopshirilganNaqd = d.TopshirilganNaqd; s.TopshirilganPlastik = d.TopshirilganPlastik; s.TopshirilganClick = d.TopshirilganClick;
    }

    private static Sotuv SotuvYarat(SotuvDto d) => new()
    {
        Id = d.Id, SmenaId = d.SmenaId, Operator = FoydalanuvchiOl(d.OperatorId, d.OperatorIsmi), Vaqt = d.Vaqt.ToLocalTime(),
    };

    private static void SotuvYangila(Sotuv s, SotuvDto d)
    {
        s.Aparat = AparatOl(d.AparatId, d.AparatRaqami, d.YoqilgiNomi);
        s.Narx = d.Narx; s.Litr = d.Litr; s.Summa = d.Summa;
        s.Tolovlar = d.Tolovlar.Select(t => new Tolov { Turi = t.Turi, Summa = t.Summa }).ToList();
        s.Holati = d.Holati; s.BekorSababi = d.BekorSababi; s.BekorQilgan = d.BekorQilgan;
    }

    /// <summary>Bitta sotuvni keshga qo'yadi. Keshda faqat bugungilar: eski sotuv (masalan, o'tgan smenadagi tahrir) qo'shilmaydi.</summary>
    private static Sotuv SotuvniQoy(SotuvDto d)
    {
        var s = Sotuvlar.FirstOrDefault(x => x.Id == d.Id);
        if (s is null)
        {
            s = SotuvYarat(d);
            if (s.Vaqt.Date == DateTime.Today)
            {
                Sotuvlar.Add(s);
                Sotuvlar.Sort((x, y) => y.Vaqt.CompareTo(x.Vaqt));
            }
        }
        SotuvYangila(s, d);
        OzgardiXabar();
        return s;
    }

    // ================= Serverdan bevosita (keshga kirmaydigan) ma'lumot =================

    /// <summary>DTO → model: keshda bo'lsa o'sha obyekt (yangilanadi), aks holda alohida obyekt.</summary>
    public static Sotuv SotuvKorinishi(SotuvDto d)
    {
        var s = Sotuvlar.FirstOrDefault(x => x.Id == d.Id) ?? SotuvYarat(d);
        SotuvYangila(s, d);
        return s;
    }

    public static Smena SmenaKorinishi(SmenaDto d)
    {
        var s = Smenalar.FirstOrDefault(x => x.Id == d.Id) ?? SmenaYarat(d);
        SmenaYangila(s, d);
        return s;
    }

    public static Foydalanuvchi FoydalanuvchiniOl(int id, string ism) => FoydalanuvchiOl(id, ism);

    public static async Task<List<Sotuv>> SmenaSotuvlari(int smenaId) =>
        (await Api.Sotuvlar(smenaId: smenaId))!.Select(SotuvKorinishi).ToList();

    /// <summary>Smenalar sahifasidagi "Hammasi" filtri — 60 kundan eskilari ham.</summary>
    public static async Task<List<Smena>> BarchaSmenalar(int? operatorId) =>
        (await Api.Smenalar(operatorId: operatorId))!.Select(SmenaKorinishi).ToList();

    /// <summary>Hamma bo'limni qayta yuklash ("Yangilash" tugmasi).</summary>
    public static Task QaytaYukla() => Yukla(Bolim.Hammasi);

    /// <summary>Klientda qilingan eksportni server auditiga yozadi (xato bo'lsa jim — fayl baribir saqlangan).</summary>
    public static async Task EksportniYoz(string turi, string tafsilot)
    {
        try
        {
            await Api.AuditEksport(new AuditEksportDto(turi, tafsilot));
            await JimYukla(Bolim.Audit);
        }
        catch (ApiXatosi) { }
    }

    private static Smena SmenaniQoy(SmenaDto d)
    {
        var s = Smenalar.FirstOrDefault(x => x.Id == d.Id);
        if (s is null)
        {
            s = SmenaYarat(d);
            Smenalar.Insert(0, s);
        }
        SmenaYangila(s, d);
        OzgardiXabar();
        return s;
    }
}
