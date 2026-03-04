using Discord;
using Discord.Interactions;
using DiscordKeyBot.Services;
using Microsoft.Extensions.Logging;

namespace DiscordKeyBot.Bot.Commands;

public sealed class RedeemCommand : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IKeyService _keyService;
    private readonly IDiscordLogService _logService;
    private readonly ILogger<RedeemCommand> _logger;

    public RedeemCommand(IKeyService keyService, IDiscordLogService logService, ILogger<RedeemCommand> logger)
    {
        _keyService = keyService;
        _logService = logService;
        _logger     = logger;
    }

    [SlashCommand("redeem", "Redeem a license key to activate it on your account")]
    public async Task RedeemAsync(
        [Summary("key", "Your license key (XXXX-XXXX-XXXX-XXXX)")]
        [MinLength(19)]
        [MaxLength(19)]
        string key)
    {
        await DeferAsync(ephemeral: true);

        var userId = Context.User.Id.ToString();
        var result = await _keyService.RedeemKeyAsync(key, userId);

        if (!result.Success)
        {
            await FollowupAsync($"❌ {result.ErrorMessage}", ephemeral: true);
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle("✅ Key Redeemed")
            .WithColor(Color.Green)
            .AddField("Key",     $"`{key.ToUpperInvariant()}`",                   inline: false)
            .AddField("Account", Context.User.Mention,                             inline: true)
            .AddField("Expires", $"<t:{Unix(result.ExpirationDate!.Value)}:F>",   inline: true)
            .WithDescription("Your license is now active. Your hardware ID will be bound on your first login.")
            .Build();

        await FollowupAsync(embed: embed, ephemeral: true);

        _ = _logService.LogKeyRedeemedAsync(key.ToUpperInvariant(), userId, result.ExpirationDate!.Value);
    }

    private static long Unix(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}
