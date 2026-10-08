using System;

namespace FuelControl.Desktop.Services;

/// <summary>Sahifalararo o'tish ("Barcha nasiyalar", "Barcha smenalar"): MainViewModel shu hodisaga obuna, ruxsatni o'zi tekshiradi.</summary>
public static class Navigatsiya
{
    public static event Action<int>? Sorov;

    /// <param name="sahifa">MainViewModel.S* konstantalaridan biri.</param>
    public static void Och(int sahifa) => Sorov?.Invoke(sahifa);

    /// <summary>Sozlamalar sahifasining bo'limi (0 narxlar, 1 aparatlar, ...) so'raldi.</summary>
    public static event Action<int>? SozlamaBolimSorovi;

    /// <summary>Sozlamalar'ga o'tib, ko'rsatilgan bo'limni ochish (masalan, bo'sh bazada "Aparatlar").</summary>
    public static void Sozlamalar(int bolim, int sozlamalarSahifasi)
    {
        SozlamaBolimSorovi?.Invoke(bolim);
        Och(sozlamalarSahifasi);
    }
}
