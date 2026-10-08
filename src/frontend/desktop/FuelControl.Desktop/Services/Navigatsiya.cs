using System;

namespace FuelControl.Desktop.Services;

/// <summary>Sahifalararo o'tish ("Barcha nasiyalar", "Barcha smenalar"): MainViewModel shu hodisaga obuna, ruxsatni o'zi tekshiradi.</summary>
public static class Navigatsiya
{
    public static event Action<int>? Sorov;

    /// <param name="sahifa">MainViewModel.S* konstantalaridan biri.</param>
    public static void Och(int sahifa) => Sorov?.Invoke(sahifa);
}
