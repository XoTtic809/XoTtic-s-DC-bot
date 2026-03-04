namespace DiscordKeyBot.Infrastructure.Configuration;

public sealed class BotConfiguration
{
    public const string SectionName = "Bot";

    public string Token { get; set; } = string.Empty;
    public ulong? GuildId { get; set; }
    public ulong LogChannelId { get; set; }
    public string AdminRoleName { get; set; } = "Admin";
}
