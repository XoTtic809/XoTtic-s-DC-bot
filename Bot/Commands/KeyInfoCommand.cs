using Discord;
using Discord.Interactions;
using DiscordKeyBot.Infrastructure.Configuration;
using DiscordKeyBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordKeyBot.Bot.Commands;

public sealed class KeyInfoCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IKeyService _keyService;
    private readonly BotConfiguration _config;
    private readonly ILogger<KeyInfoCommand> _logger;

    public KeyInfoCommand(IKeyService keyService, IOptions<BotConfiguration> config, ILogger<KeyInfoCommand> logger)
    {
        _keyService = keyService;
        _config     = config.Value;
        _logger     = logger;
    }

    [SlashCommand("keyinfo", "Look up a license key (Admin only)")]
    public async Task KeyInfoAsync(
        [Summary("key", "The license key to look up (XXXX-XXXX-XXXX-XXXX)")]
        [MinLength(19)]
        [MaxLength(19)]
        string key)
    {
        await DeferAsync(ephemeral: true);

        if (!IsAdmin())
        {
            await FollowupAsync("❌ You don't have permission to view key details.", ephemeral: true);
            return;
        }

        var result = await _keyService.GetKeyInfoAsync(key);

        if (!result.Found || result.KeyData is null)
        {
            await FollowupAsync($"❌ {result.ErrorMessage}", ephemeral: true);
            return;
        }

        var data = result.KeyData;

        var (status, color) = data switch
        {
            { IsRevoked: true }        => ("🚫 Revoked", Color.Red),
            { } d when d.IsExpired    => ("⌛ Expired",  Color.Orange),
            { IsUsed: false }          => ("🆕 Unused",  Color.LightGrey),
            _                          => ("✅ Active",   Color.Green)
        };

        var embed = new EmbedBuilder()
            .WithTitle("🔍 Key Info")
            .WithColor(color)
            .AddField("Key",    $"`{data.Key}`", inline: false)
            .AddField("Status", status,           inline: true)
            .AddField("Expires",
                data.ExpirationDate > DateTime.UtcNow.AddYears(50)
                    ? "Lifetime"
                    : $"<t:{Unix(data.ExpirationDate)}:F>",              inline: true)
            .AddField("\u200B", "\u200B",                                 inline: false)
            .AddField("Redeemed",
                data.IsUsed ? $"<t:{Unix(data.RedeemedAt!.Value)}:R>" : "No", inline: true)
            .AddField("Discord User",
                data.DiscordUserId is not null ? $"<@{data.DiscordUserId}>" : "None", inline: true)
            .AddField("HWID",
                data.HWID is not null ? $"`{data.HWID}`" : "Not bound",  inline: false)
            .AddField("Created", $"<t:{Unix(data.CreatedAt)}:F>",        inline: true)
            .AddField("ID",      $"`{data.Id}`",                          inline: false)
            .WithFooter("Only visible to you.")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);
    }

    private bool IsAdmin()
    {
        if (Context.User is not IGuildUser guildUser) return false;
        if (guildUser.GuildPermissions.Administrator) return true;

        return guildUser.RoleIds
            .Select(id => Context.Guild.GetRole(id))
            .Any(r => r?.Name.Equals(_config.AdminRoleName, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static long Unix(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}
