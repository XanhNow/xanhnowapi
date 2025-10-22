using System.Security.Claims;
using AuthService.Application.Abstractions;
using AuthService.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _svc;
    public AuthController(IAuthService svc) { _svc = svc; }

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponse>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var res = await _svc.RegisterAsync(req, ip, ct);
        return Ok(res);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var res = await _svc.LoginAsync(req, ip, ct);
        return Ok(res);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        var res = await _svc.RefreshAsync(req, ip, ct);
        return Ok(res);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _svc.LogoutAsync(uid, req.RefreshToken, ct);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _svc.ChangePasswordAsync(uid, req, ct);
        return NoContent();
    }

    [HttpPost("forgot/start")]
    public async Task<IActionResult> ForgotStart([FromBody] ForgotPasswordStartRequest req, CancellationToken ct)
    {
        await _svc.StartForgotPasswordAsync(req, ct);
        return NoContent();
    }

    [HttpPost("forgot/verify")]
    public async Task<IActionResult> ForgotVerify([FromBody] ForgotPasswordVerifyRequest req, CancellationToken ct)
    {
        await _svc.CompleteForgotPasswordAsync(req, ct);
        return NoContent();
    }
}
