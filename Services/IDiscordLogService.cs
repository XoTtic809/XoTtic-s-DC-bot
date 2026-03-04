using DiscordKeyBot.Models;

namespace DiscordKeyBot.Services;

public interface IDiscordLogService
{
    Task LogKeyGeneratedAsync(string key, KeyType type, string generatedBy, DateTime expiration);
    Task LogKeyRedeemedAsync(string key, string discordUserId, DateTime expiration);
    Task LogKeyRevokedAsync(string key, string revokedBy);
    Task LogInfoAsync(string title, string message);
    Task LogErrorAsync(string title, string message);
}
