namespace AuthService.Infrastructure.Caching;

public static class RedisKeys
{
    public static string PasskeyChallenge(string key) => $"passkey:challenge:{key}";
    public static string ForgotPasswordCode(string phone) => $"pwdreset:{phone}";
    public static string RevokedJwtJti(string jti) => $"jwt:revoked:{jti}";
}
