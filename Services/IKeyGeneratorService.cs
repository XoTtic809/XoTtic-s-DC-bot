using DiscordKeyBot.Models;

namespace DiscordKeyBot.Services;

public interface IKeyGeneratorService
{
    string GenerateKey();
    DateTime GetExpirationDate(KeyType type);
}
