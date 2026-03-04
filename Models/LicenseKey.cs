namespace DiscordKeyBot.Models;

public class LicenseKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public bool IsUsed { get; set; } = false;
    public string? DiscordUserId { get; set; }
    public string? HWID { get; set; }
    public DateTime ExpirationDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RedeemedAt { get; set; }
    public bool IsRevoked { get; set; } = false;

    public bool IsExpired => DateTime.UtcNow > ExpirationDate;
}
