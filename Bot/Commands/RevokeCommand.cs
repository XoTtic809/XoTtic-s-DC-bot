using Discord;
using Discord.Interactions;
using DiscordKeyBot.Infrastructure.Configuration;
using DiscordKeyBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordKeyBot.Bot.Commands;

public sealed class RevokeCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IKeyService _keyService;
    private readonly IDiscordLogService _logService;
    private readonly BotConfiguration _config;
    private readonly ILogger<RevokeCommand> _logger;

    public RevokeCommand(IKeyService keyService, IDiscordLogService logService, IOptions<BotConfiguration> config, ILogger<RevokeCommand> logger)
    {
        _keyService = keyService;
        _logService = logService;
        _config     = config.Value;
        _logger     = logger;
    }

    [SlashCommand("revoke", "Revoke a license key (Admin only)")]
    public async Task RevokeAsync(
        [Summary("key", "The license key to revoke (XXXX-XXXX-XXXX-XXXX)")]
        [MinLength(19)]
        [MaxLength(19)]
        string key)
    {
        await DeferAsync(ephemeral: true);

        if (!IsAdmin())
        {
            await FollowupAsync("❌ You don't have permission to revoke keys.", ephemeral: true);
            return;
        }

        var result = await _keyService.RevokeKeyAsync(key);

        if (!result.Success)
        {
            await FollowupAsync($"❌ {result.ErrorMessage}", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("🚫 Key Revoked")
            .WithColor(Color.Red)
            .AddField("Key",        $"`{key.ToUpperInvariant()}`", inline: false)
            .AddField("Revoked By", Context.User.Username,         inline: true)
            .WithDescription("This key is permanently invalidated. Any active user will receive `REVOKED` on their next verify call.")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);

        _ = _logService.LogKeyRevokedAsync(key.ToUpperInvariant(), Context.User.Username);
    }

    private bool IsAdmin()
    {
        if (Context.User is not IGuildUser guildUser) return false;
        if (guildUser.GuildPermissions.Administrator) return true;

        return guildUser.RoleIds
            .Select(id => Context.Guild.GetRole(id))
            .Any(r => r?.Name.Equals(_config.AdminRoleName, StringComparison.OrdinalIgnoreCase) == true);
    }
}
