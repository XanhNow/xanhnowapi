using System.Security.Claims;
using AuthService.Infrastructure.Services;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PasskeyController : ControllerBase
{
    private readonly PasskeyService _svc;
    public PasskeyController(PasskeyService svc) { _svc = svc; }

    [Authorize]
    [HttpPost("attestation/options")]
    public async Task<ActionResult<CredentialCreateOptions>> BeginRegister(CancellationToken ct)
    {
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var display = User.FindFirst("fullname")?.Value ?? "User";
        var username = User.FindFirst("phone")?.Value ?? uid.ToString();
        var options = await _svc.BeginRegisterAsync(uid, display, username, ct);
        return Ok(options);
    }

    [Authorize]
    [HttpPost("attestation/verify")]
    public async Task<ActionResult> CompleteRegister([FromBody] AuthenticatorAttestationRawResponse attResp, [FromQuery] string challenge, CancellationToken ct)
    {
        var uid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _svc.CompleteRegisterAsync(attResp, challenge, uid, ct);
        return NoContent();
    }

    [HttpPost("assertion/options")]
    public async Task<ActionResult<AssertionOptions>> BeginLogin([FromBody] Guid userId, CancellationToken ct)
    {
        var options = await _svc.BeginLoginAsync(userId, ct);
        return Ok(options);
    }

    [HttpPost("assertion/verify")]
    public async Task<ActionResult> CompleteLogin([FromBody] AuthenticatorAssertionRawResponse assnResp, [FromQuery] string challenge, CancellationToken ct)
    {
        await _svc.CompleteLoginAsync(assnResp, challenge, ct);
        return NoContent();
    }
}
