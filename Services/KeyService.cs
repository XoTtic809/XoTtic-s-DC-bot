using DiscordKeyBot.Data;
using DiscordKeyBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordKeyBot.Services;

public sealed class KeyService : IKeyService
{
    private readonly AppDbContext _db;
    private readonly IKeyGeneratorService _generator;
    private readonly ILogger<KeyService> _logger;

    public KeyService(AppDbContext db, IKeyGeneratorService generator, ILogger<KeyService> logger)
    {
        _db        = db;
        _generator = generator;
        _logger    = logger;
    }

    public async Task<GenerateResult> GenerateKeyAsync(KeyType type, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var keyString  = _generator.GenerateKey();
            var expiration = _generator.GetExpirationDate(type);

            if (await _db.LicenseKeys.AnyAsync(k => k.Key == keyString, ct))
            {
                _logger.LogWarning("Key collision on attempt {Attempt}", attempt);
                continue;
            }

            _db.LicenseKeys.Add(new LicenseKey
            {
                Key            = keyString,
                ExpirationDate = expiration,
                CreatedAt      = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Generated {Type} key {Key}", type, keyString);
            return new GenerateResult(true, keyString, null, expiration);
        }

        return new GenerateResult(false, null, "Failed to generate a unique key, please try again.");
    }

    public async Task<RedeemResult> RedeemKeyAsync(string key, string discordUserId, CancellationToken ct = default)
    {
        key = key.Trim().ToUpperInvariant();

        var license = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key, ct);

        if (license is null)
            return new RedeemResult(false, "That key does not exist.", null);

        if (license.IsRevoked)
            return new RedeemResult(false, "That key has been revoked.", null);

        if (license.IsExpired)
            return new RedeemResult(false, $"That key expired on {license.ExpirationDate:R}.", null);

        if (license.IsUsed)
            return new RedeemResult(false, "That key has already been redeemed.", null);

        license.IsUsed        = true;
        license.DiscordUserId = discordUserId;
        license.RedeemedAt    = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Key {Key} redeemed by {UserId}", key, discordUserId);
        return new RedeemResult(true, null, license.ExpirationDate);
    }

    public async Task<RevokeResult> RevokeKeyAsync(string key, CancellationToken ct = default)
    {
        key = key.Trim().ToUpperInvariant();

        var license = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key, ct);

        if (license is null)
            return new RevokeResult(false, "That key does not exist.");

        if (license.IsRevoked)
            return new RevokeResult(false, "That key is already revoked.");

        license.IsRevoked = true;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Key {Key} revoked", key);
        return new RevokeResult(true, null);
    }

    public async Task<KeyInfoResult> GetKeyInfoAsync(string key, CancellationToken ct = default)
    {
        key = key.Trim().ToUpperInvariant();

        var license = await _db.LicenseKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.Key == key, ct);

        return license is null
            ? new KeyInfoResult(false, null, "Key not found.")
            : new KeyInfoResult(true, license, null);
    }

    public async Task<VerifyResult> VerifyKeyAsync(string key, string hwid, CancellationToken ct = default)
    {
        key  = key.Trim().ToUpperInvariant();
        hwid = hwid.Trim();

        if (string.IsNullOrWhiteSpace(hwid))
            return new VerifyResult(VerifyStatus.INVALID, null);

        var license = await _db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key, ct);

        if (license is null)     return new VerifyResult(VerifyStatus.INVALID, null);
        if (license.IsRevoked)   return new VerifyResult(VerifyStatus.REVOKED, null);
        if (license.IsExpired)   return new VerifyResult(VerifyStatus.EXPIRED, license.ExpirationDate);

        // First verify call — bind the HWID
        if (string.IsNullOrWhiteSpace(license.HWID))
        {
            license.HWID = hwid;
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Key {Key} HWID bound", key);
        }
        else if (!string.Equals(license.HWID, hwid, StringComparison.Ordinal))
        {
            _logger.LogWarning("Key {Key} HWID mismatch", key);
            return new VerifyResult(VerifyStatus.HWID_MISMATCH, null);
        }

        return new VerifyResult(VerifyStatus.VALID, license.ExpirationDate);
    }
}
