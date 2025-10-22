namespace AuthService.Application.Contracts;

public record RegisterRequest(string FullName, string PhoneNumber, string Password);
public record LoginRequest(string PhoneNumber, string Password);
public record TokenResponse(string AccessToken, string RefreshToken, long ExpiresInSeconds);
public record RefreshRequest(string RefreshToken);
public record UpdateProfileRequest(string FullName);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ForgotPasswordStartRequest(string PhoneNumber);
public record ForgotPasswordVerifyRequest(string PhoneNumber, string Code, string NewPassword);
