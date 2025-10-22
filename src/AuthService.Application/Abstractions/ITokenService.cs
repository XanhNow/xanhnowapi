using AuthService.Domain.Entities;

namespace AuthService.Application.Abstractions;

public interface ITokenService
{
    (string AccessToken, long ExpiresInSeconds, string JwtId) CreateJwt(ApplicationUser user, IEnumerable<(string Type, string Value)>? extraClaims = null);
    string GenerateSecureRefreshToken();
}
