namespace FuelControl.Contracts.Dto;

public sealed record LoginSoroviDto(string Login, string ParolYokiPin);

public sealed record LoginJavobiDto(string Token, DateTime TokenMuddati, FoydalanuvchiDto Foydalanuvchi);
