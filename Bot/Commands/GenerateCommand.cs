using Discord;
using Discord.Interactions;
using DiscordKeyBot.Infrastructure.Configuration;
using DiscordKeyBot.Models;
using DiscordKeyBot.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordKeyBot.Bot.Commands;

public sealed class GenerateCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IKeyService _keyService;
    private readonly IDiscordLogService _logService;
    private readonly BotConfiguration _config;
    private readonly ILogger<GenerateCommand> _logger;

    public GenerateCommand(IKeyService keyService, IDiscordLogService logService, IOptions<BotConfiguration> config, ILogger<GenerateCommand> logger)
    {
        _keyService = keyService;
        _logService = logService;
        _config     = config.Value;
        _logger     = logger;
    }

    [SlashCommand("generate", "Generate a new license key (Admin only)")]
    public async Task GenerateAsync([Summary("type", "Duration of the license")] KeyTypeChoice type)
    {
        await DeferAsync(ephemeral: true);

        if (!IsAdmin())
        {
            await FollowupAsync("❌ You don't have permission to generate keys.", ephemeral: true);
            return;
        }

        var keyType = type switch
        {
            KeyTypeChoice.Day      => KeyType.Day,
            KeyTypeChoice.Week     => KeyType.Week,
            KeyTypeChoice.Month    => KeyType.Month,
            KeyTypeChoice.Lifetime => KeyType.Lifetime,
            _                      => KeyType.Day
        };

        var result = await _keyService.GenerateKeyAsync(keyType);

        if (!result.Success)
        {
            _logger.LogError("Key generation failed: {Error}", result.ErrorMessage);
            await FollowupAsync($"❌ {result.ErrorMessage}", ephemeral: true);
            return;
        }

        var expiration = result.ExpirationDate ?? DateTime.UtcNow;

        var embed = new EmbedBuilder()
            .WithTitle("🔑 License Key Generated")
            .WithColor(Color.Green)
            .AddField("Key",     $"```{result.Key}```",              inline: false)
            .AddField("Type",    type.ToString(),                     inline: true)
            .AddField("Expires", $"<t:{Unix(expiration)}:F>",        inline: true)
            .WithFooter("Keep this private — share it only with the intended user.")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);

        _ = _logService.LogKeyGeneratedAsync(result.Key!, keyType, Context.User.Username, expiration);
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

public enum KeyTypeChoice
{
    [ChoiceDisplay("Day (24 hours)")]   Day,
    [ChoiceDisplay("Week (7 days)")]    Week,
    [ChoiceDisplay("Month (30 days)")] Month,
    [ChoiceDisplay("Lifetime")]        Lifetime
}
