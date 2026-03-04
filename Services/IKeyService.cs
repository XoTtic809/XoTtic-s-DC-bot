using DiscordKeyBot.Models;

namespace DiscordKeyBot.Services;

public record GenerateResult(bool Success, string? Key, string? ErrorMessage, DateTime? ExpirationDate = null);
public record RedeemResult(bool Success, string? ErrorMessage, DateTime? ExpirationDate);
public record RevokeResult(bool Success, string? ErrorMessage);
public record KeyInfoResult(bool Found, LicenseKey? KeyData, string? ErrorMessage);
public record VerifyResult(VerifyStatus Status, DateTime? ExpirationDate);

public interface IKeyService
{
    Task<GenerateResult> GenerateKeyAsync(KeyType type, CancellationToken ct = default);
    Task<RedeemResult> RedeemKeyAsync(string key, string discordUserId, CancellationToken ct = default);
    Task<RevokeResult> RevokeKeyAsync(string key, CancellationToken ct = default);
    Task<KeyInfoResult> GetKeyInfoAsync(string key, CancellationToken ct = default);
    Task<VerifyResult> VerifyKeyAsync(string key, string hwid, CancellationToken ct = default);
}
