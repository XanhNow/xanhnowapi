using AuthService.Application.Options;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Caching;
using AuthService.Infrastructure.Persistence;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AuthService.Infrastructure.Services;

public class PasskeyService
{
    private static string ToBase64Url(byte[] data)
    {
        var s = Convert.ToBase64String(data);
        s = s.TrimEnd('=').Replace('+','-').Replace('/','_');
        return s;
    }

    private static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    private readonly AppDbContext _db;
    private readonly IDistributedCache _cache;
    private readonly Fido2 _fido2;

    public PasskeyService(AppDbContext db, IDistributedCache cache, IOptions<PasskeyOptions> opt)
    {
        _db = db; _cache = cache;
        var o = opt.Value;
        _fido2 = new Fido2(new Fido2Configuration
        {
            ServerDomain = o.RelyingPartyId,
            ServerName = o.RelyingPartyName,
            Origins = o.AllowedOrigins?.ToHashSet() ?? new HashSet<string>()
        });
    }

    public async Task<CredentialCreateOptions> BeginRegisterAsync(Guid userId, string displayName, string username, CancellationToken ct)
    {
        var user = new Fido2User
        {
            Id = Encoding.UTF8.GetBytes(userId.ToString()),
            Name = username,
            DisplayName = displayName
        };

        var existing = await _db.PasskeyCredentials
            .Where(x => x.UserId == userId)
            .Select(x => new PublicKeyCredentialDescriptor(x.DescriptorId))
            .ToListAsync(ct);

        var authenticatorSelection = new AuthenticatorSelection
        {
            RequireResidentKey = false,
            UserVerification = UserVerificationRequirement.Preferred,
            AuthenticatorAttachment = AuthenticatorAttachment.CrossPlatform
        };

        var exts = new AuthenticationExtensionsClientInputs { Extensions = true, UserVerificationMethod = true };

        var options = _fido2.RequestNewCredential(user, existing, authenticatorSelection, AttestationConveyancePreference.None, exts);

        var cacheKey = RedisKeys.PasskeyChallenge(ToBase64Url(options.Challenge));
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(options), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);

        return options;
    }

    public async Task CompleteRegisterAsync(AuthenticatorAttestationRawResponse attResp, string challengeB64, Guid userId, CancellationToken ct)
    {
        var cacheKey = RedisKeys.PasskeyChallenge(challengeB64);
        var json = await _cache.GetStringAsync(cacheKey, ct) ?? throw new InvalidOperationException("Challenge expired");
        var options = JsonSerializer.Deserialize<CredentialCreateOptions>(json)!;
        var result = await _fido2.MakeNewCredentialAsync(attResp, options, (args, token) => Task.FromResult(true));
        if (result.Result == null)
            throw new InvalidOperationException("Credential creation failed: result is null.");
        var cred = new PasskeyCredential
        {
            UserId = userId,
            DescriptorId = result.Result.CredentialId,
            PublicKey = result.Result.PublicKey,
            UserHandle = result.Result.User.Id,
            SignCount = result.Result.Counter,
            CredType = result.Result.CredType,
            Aaguid = result.Result.Aaguid
        };
        _db.PasskeyCredentials.Add(cred);
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(cacheKey, ct);
        return;
    }

    public async Task<AssertionOptions> BeginLoginAsync(Guid userId, CancellationToken ct)
    {
        var allowed = await _db.PasskeyCredentials.Where(x => x.UserId == userId)
            .Select(x => new PublicKeyCredentialDescriptor(x.DescriptorId))
            .ToListAsync(ct);

        var exts = new AuthenticationExtensionsClientInputs { UserVerificationMethod = true };
        var options = _fido2.GetAssertionOptions(allowed, UserVerificationRequirement.Preferred, exts);

        var cacheKey = RedisKeys.PasskeyChallenge(ToBase64Url(options.Challenge));
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(options), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);

        return options;
    }

    public async Task CompleteLoginAsync(AuthenticatorAssertionRawResponse assnResp, string challengeB64, CancellationToken ct)
    {
        var cacheKey = RedisKeys.PasskeyChallenge(challengeB64);
        var json = await _cache.GetStringAsync(cacheKey, ct) ?? throw new InvalidOperationException("Challenge expired");
        var options = JsonSerializer.Deserialize<AssertionOptions>(json)!;
        var creds = await _db.PasskeyCredentials.Where(x => true).ToListAsync(ct);

        var credential = creds.FirstOrDefault(x => x.DescriptorId.SequenceEqual(assnResp.RawId))
                     ?? throw new InvalidOperationException("Unknown credential");

        var res = await _fido2.MakeAssertionAsync(assnResp, options, credential.PublicKey, credential.SignCount,
            (args, token) => Task.FromResult(credential.DescriptorId.SequenceEqual(args.CredentialId)));
        credential.SignCount = res.Counter;
        credential.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _cache.RemoveAsync(cacheKey, ct);
        return;
    }
}
