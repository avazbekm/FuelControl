using System;
using System.Threading;
using System.Threading.Tasks;

namespace FuelControl.Desktop.Services;

/// <summary>
/// Yozish paytidagi so'rovlar uchun kechiktirish (debounce): Rejala() har chaqirilganda oldingisi bekor bo'ladi, oxirgisidan
/// ms o'tgach ish bajariladi. DispatcherTimer'siz — Task.Delay va chaqiruvchi (UI) kontekstida davom etadi, shuning uchun
/// obyekt qaysi oqimda yaratilganiga bog'liq emas (dialog VM'lari statik ravishda yaratiladi).
/// Parametr — "hali dolzarbmi?" tekshiruvi: natijani qo'llashdan oldin chaqiring.
/// </summary>
public sealed class Kechiktirgich(Func<Func<bool>, Task> ish, int millisoniya = 250)
{
    private CancellationTokenSource? _joriy;

    public async void Rejala()
    {
        _joriy?.Cancel();
        var c = _joriy = new CancellationTokenSource();
        try { await Task.Delay(millisoniya, c.Token); }
        catch (TaskCanceledException) { return; }
        try { await ish(() => c == _joriy && !c.IsCancellationRequested); }
        catch (ApiXatosi) { /* aloqa yo'q yoki ruxsat yo'q — eski ko'rinish qoladi */ }
    }

    public void BekorQil() => _joriy?.Cancel();
}
