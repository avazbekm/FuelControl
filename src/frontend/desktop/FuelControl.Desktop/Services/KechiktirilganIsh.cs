using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace FuelControl.Desktop.Services;

/// <summary>
/// Serverdan yuklaydigan sahifalar uchun: ketma-ket kelgan so'rovlar (filtr o'zgarishi, SignalR xabarlari) bitta yuklashga
/// birlashtiriladi; eskirgan javob (keyinroq boshqa yuklash boshlangan bo'lsa) qo'llanmaydi.
/// Kechiktirish DispatcherTimer emas, Task.Delay bilan (Kechiktirgich kabi): taymer ba'zi holatlarda ishga tushmay,
/// Smenalar'da boshqa smena tanlanganda tafsilot yuklanmay qolardi.
/// </summary>
public sealed class KechiktirilganIsh
{
    private readonly Func<Func<bool>, Task> _ish;
    private readonly int _millisoniya;
    private int _versiya;
    private int _rejaVersiyasi;

    /// <param name="ish">Yuklash. Parametr — "hali dolzarbmi?" tekshiruvi: natijani qo'llashdan oldin chaqiring.</param>
    public KechiktirilganIsh(Func<Func<bool>, Task> ish, int millisoniya = 300)
    {
        _ish = ish;
        _millisoniya = millisoniya;
    }

    public void Rejala()
    {
        var r = ++_rejaVersiyasi;
        _ = Task.Delay(_millisoniya).ContinueWith(_ => Dispatcher.UIThread.Post(async () =>
        {
            if (r == _rejaVersiyasi) await Bajar();
        }));
    }

    /// <summary>Kutmasdan darhol yuklash (masalan, "Yangilash" tugmasi).</summary>
    public async Task Bajar()
    {
        ++_rejaVersiyasi;   // rejadagi (kutilayotgan) yuklash bekor
        var v = ++_versiya;
        try { await _ish(() => v == _versiya); }
        catch (ApiXatosi) { /* aloqa yo'q yoki ruxsat yo'q — eski ko'rinish qoladi */ }
    }
}
