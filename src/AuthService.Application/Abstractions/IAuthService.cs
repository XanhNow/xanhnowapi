using AuthService.Application.Contracts;

namespace AuthService.Application.Abstractions;

public interface IAuthService
{
    Task<TokenResponse> RegisterAsync(RegisterRequest req, string ip, CancellationToken ct);
    Task<TokenResponse> LoginAsync(LoginRequest req, string ip, CancellationToken ct);
    Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct);
    Task<TokenResponse> RefreshAsync(RefreshRequest req, string ip, CancellationToken ct);
    Task UpdateProfileAsync(Guid userId, UpdateProfileRequest req, CancellationToken ct);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest req, CancellationToken ct);
    Task StartForgotPasswordAsync(ForgotPasswordStartRequest req, CancellationToken ct);
    Task CompleteForgotPasswordAsync(ForgotPasswordVerifyRequest req, CancellationToken ct);
}
