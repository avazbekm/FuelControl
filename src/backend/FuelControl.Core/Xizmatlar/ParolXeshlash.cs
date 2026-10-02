using System.Security.Cryptography;

namespace FuelControl.Core.Xizmatlar;

/// <summary>PBKDF2-SHA256 bilan parol/PIN xeshlash. Format: "iteratsiya.tuzBase64.xeshBase64".</summary>
public static class ParolXeshlash
{
    private const int TuzUzunligi = 16;
    private const int XeshUzunligi = 32;
    private const int Iteratsiya = 100_000;

    public static string Xeshla(string parol)
    {
        var tuz = RandomNumberGenerator.GetBytes(TuzUzunligi);
        var xesh = Rfc2898DeriveBytes.Pbkdf2(parol, tuz, Iteratsiya, HashAlgorithmName.SHA256, XeshUzunligi);
        return $"{Iteratsiya}.{Convert.ToBase64String(tuz)}.{Convert.ToBase64String(xesh)}";
    }

    public static bool Tekshir(string parol, string xeshQatori)
    {
        var qismlar = xeshQatori.Split('.');
        if (qismlar.Length != 3) return false;
        if (!int.TryParse(qismlar[0], out var iteratsiya)) return false;

        byte[] tuz, kutilgan;
        try
        {
            tuz = Convert.FromBase64String(qismlar[1]);
            kutilgan = Convert.FromBase64String(qismlar[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var hisoblangan = Rfc2898DeriveBytes.Pbkdf2(parol, tuz, iteratsiya, HashAlgorithmName.SHA256, kutilgan.Length);
        return CryptographicOperations.FixedTimeEquals(hisoblangan, kutilgan);
    }
}
