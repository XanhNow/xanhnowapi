namespace AuthService.Application.Options;

public class JwtOptions
{
    public string Issuer { get; set; } = "xanhnow-auth";
    public string Audience { get; set; } = "xanhnow";
    public string SecretKey { get; set; } = default!;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}
