namespace FuelControl.Contracts.Dto;

public sealed record FoydalanuvchiDto(
    int Id,
    string ToliqIsm,
    string Login,
    Rol Rol,
    bool Faol,
    long OylikMaosh,
    Ruxsat[] Ruxsatlar);

public sealed record FoydalanuvchiYaratishDto(
    string ToliqIsm,
    string Login,
    Rol Rol,
    long OylikMaosh,
    string ParolYokiPin);

/// <summary>Login berilsa — o'zgartiriladi (band bo'lmasligi kerak).</summary>
public sealed record FoydalanuvchiTahrirlashDto(
    string ToliqIsm,
    Rol Rol,
    bool Faol,
    long OylikMaosh,
    string? Login = null);

public sealed record RuxsatlarOrnatishDto(Ruxsat[] Ruxsatlar);

public sealed record PinOrnatishDto(string YangiParolYokiPin);
