using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace FuelControl.Desktop.Services;

/// <summary>
/// Serverdan yuklaydigan sahifalar uchun: ketma-ket kelgan so'rovlar (filtr o'zgarishi, SignalR xabarlari) bitta yuklashga
/// birlashtiriladi; eskirgan javob (keyinroq boshqa yuklash boshlangan bo'lsa) qo'llanmaydi.
/// </summary>
public sealed class KechiktirilganIsh
{
    private readonly Func<Func<bool>, Task> _ish;
    private readonly DispatcherTimer _taymer;
    private int _versiya;

    /// <param name="ish">Yuklash. Parametr — "hali dolzarbmi?" tekshiruvi: natijani qo'llashdan oldin chaqiring.</param>
    public KechiktirilganIsh(Func<Func<bool>, Task> ish, int millisoniya = 300)
    {
        _ish = ish;
        _taymer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(millisoniya) };
        _taymer.Tick += async (_, _) => { _taymer.Stop(); await Bajar(); };
    }

    public void Rejala()
    {
        _taymer.Stop();
        _taymer.Start();
    }

    /// <summary>Kutmasdan darhol yuklash (masalan, "Yangilash" tugmasi).</summary>
    public async Task Bajar()
    {
        _taymer.Stop();
        var v = ++_versiya;
        try { await _ish(() => v == _versiya); }
        catch (ApiXatosi) { /* aloqa yo'q yoki ruxsat yo'q — eski ko'rinish qoladi */ }
    }
}
