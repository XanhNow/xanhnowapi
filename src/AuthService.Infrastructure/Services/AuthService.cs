using System.Security.Claims;
using AuthService.Application.Abstractions;
using AuthService.Application.Contracts;
using AuthService.Domain.Abstractions;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Caching;
using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace AuthService.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ITokenService _token;
    private readonly AppDbContext _db;
    private readonly IRepository<RefreshToken> _refreshRepo;
    private readonly IUnitOfWork _uow;
    private readonly IDistributedCache _cache;
    private readonly IEventBus _bus;

    public AuthService(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        ITokenService token,
        AppDbContext db,
        IRepository<RefreshToken> refreshRepo,
        IUnitOfWork uow,
        IDistributedCache cache,
        IEventBus bus)
    {
        _users = users; _signIn = signIn; _token = token; _db = db;
        _refreshRepo = refreshRepo; _uow = uow; _cache = cache; _bus = bus;
    }

    public async Task<TokenResponse> RegisterAsync(RegisterRequest req, string ip, CancellationToken ct)
    {
        var exists = await _users.Users.AnyAsync(x => x.PhoneNumber == req.PhoneNumber, ct);
        if (exists) throw new InvalidOperationException("PhoneNumber already registered.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = req.PhoneNumber,
            PhoneNumber = req.PhoneNumber,
            FullName = req.FullName,
            PhoneNumberConfirmed = false,
            EmailConfirmed = false,
            IsActive = true
        };

        var create = await _users.CreateAsync(user, req.Password);
        if (!create.Succeeded) throw new InvalidOperationException(string.Join("; ", create.Errors.Select(e => e.Description)));

        var (access, ttl, jti) = _token.CreateJwt(user);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _token.GenerateSecureRefreshToken(),
            JwtId = jti,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedByIp = ip
        };
        await _refreshRepo.AddAsync(refresh, ct);
        await _uow.SaveChangesAsync(ct);

        _ = _bus.PublishAsync("user.registered", new { user.Id, user.PhoneNumber, user.FullName, At = DateTime.UtcNow });
        return new TokenResponse(access, refresh.Token, ttl);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest req, string ip, CancellationToken ct)
    {
        var user = await _users.Users.FirstOrDefaultAsync(x => x.PhoneNumber == req.PhoneNumber, ct);
        if (user == null || !user.IsActive) throw new InvalidOperationException("Invalid credentials.");

        var check = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!check.Succeeded) throw new InvalidOperationException("Invalid credentials.");

        var (access, ttl, jti) = _token.CreateJwt(user);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _token.GenerateSecureRefreshToken(),
            JwtId = jti,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedByIp = ip
        };
        await _refreshRepo.AddAsync(refresh, ct);
        await _uow.SaveChangesAsync(ct);

        _ = _bus.PublishAsync("user.loggedin", new { user.Id, user.PhoneNumber, At = DateTime.UtcNow });
        return new TokenResponse(access, refresh.Token, ttl);
    }

    public async Task LogoutAsync(Guid userId, string refreshToken, CancellationToken ct)
    {
        var rt = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.UserId == userId && x.Token == refreshToken, ct);
        if (rt == null) return;
        rt.RevokedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest req, string ip, CancellationToken ct)
    {
        var old = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == req.RefreshToken, ct);
        if (old == null || old.IsExpired || old.IsRevoked) throw new InvalidOperationException("Invalid refresh token.");

        var user = await _users.FindByIdAsync(old.UserId.ToString());
        if (user == null || !user.IsActive) throw new InvalidOperationException("User not found.");

        old.RevokedAt = DateTime.UtcNow;
        var (access, ttl, jti) = _token.CreateJwt(user);
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            Token = _token.GenerateSecureRefreshToken(),
            JwtId = jti,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedByIp = ip,
            ReplacedByToken = old.Token
        };
        await _refreshRepo.AddAsync(refresh, ct);
        await _uow.SaveChangesAsync(ct);

        return new TokenResponse(access, refresh.Token, ttl);
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest req, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        user.FullName = req.FullName;
        var res = await _users.UpdateAsync(user);
        if (!res.Succeeded) throw new InvalidOperationException(string.Join("; ", res.Errors.Select(e => e.Description)));
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(userId.ToString()) ?? throw new InvalidOperationException("User not found.");
        var res = await _users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!res.Succeeded) throw new InvalidOperationException(string.Join("; ", res.Errors.Select(e => e.Description)));
        _ = _bus.PublishAsync("user.password.changed", new { user.Id, At = DateTime.UtcNow });
    }

    public async Task StartForgotPasswordAsync(ForgotPasswordStartRequest req, CancellationToken ct)
    {
        var user = await _users.Users.FirstOrDefaultAsync(x => x.PhoneNumber == req.PhoneNumber, ct);
        if (user == null) return;
        var code = Random.Shared.Next(100000, 999999).ToString();
        await _cache.SetStringAsync(RedisKeys.ForgotPasswordCode(req.PhoneNumber), code,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) }, ct);
        Console.WriteLine($"[DEV] Reset code for {req.PhoneNumber}: {code}");
    }

    public async Task CompleteForgotPasswordAsync(ForgotPasswordVerifyRequest req, CancellationToken ct)
    {
        var saved = await _cache.GetStringAsync(RedisKeys.ForgotPasswordCode(req.PhoneNumber), ct);
        if (saved == null || saved != req.Code) throw new InvalidOperationException("Invalid or expired code.");
        var user = await _users.Users.FirstOrDefaultAsync(x => x.PhoneNumber == req.PhoneNumber, ct) 
                   ?? throw new InvalidOperationException("User not found.");
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var res = await _users.ResetPasswordAsync(user, token, req.NewPassword);
        if (!res.Succeeded) throw new InvalidOperationException(string.Join("; ", res.Errors.Select(e => e.Description)));
        await _cache.RemoveAsync(RedisKeys.ForgotPasswordCode(req.PhoneNumber), ct);
    }
}
