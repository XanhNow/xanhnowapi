namespace AuthService.Application.Options;

public class PasskeyOptions
{
    public string RelyingPartyId { get; set; } = "auth.local";
    public string RelyingPartyName { get; set; } = "XanhNow Auth";
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}
