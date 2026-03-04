using DiscordKeyBot.Models;
using System.Security.Cryptography;

namespace DiscordKeyBot.Services;

public sealed class KeyGeneratorService : IKeyGeneratorService
{
    // Excludes visually ambiguous chars (0/O, 1/I/L) to prevent typos when typing keys manually.
    // 256 % 32 == 0, so no modulo bias when mapping random bytes to this alphabet.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string GenerateKey()
    {
        var bytes    = new byte[16];
        RandomNumberGenerator.Fill(bytes);

        var segments = new string[4];
        for (var s = 0; s < 4; s++)
        {
            var chars = new char[4];
            for (var c = 0; c < 4; c++)
                chars[c] = Alphabet[bytes[s * 4 + c] % Alphabet.Length];
            segments[s] = new string(chars);
        }

        return string.Join("-", segments);
    }

    public DateTime GetExpirationDate(KeyType type) => type switch
    {
        KeyType.Day      => DateTime.UtcNow.AddDays(1),
        KeyType.Week     => DateTime.UtcNow.AddDays(7),
        KeyType.Month    => DateTime.UtcNow.AddDays(30),
        KeyType.Lifetime => DateTime.UtcNow.AddYears(100),
        _                => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
