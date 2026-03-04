using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordKeyBot.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordKeyBot.Bot;

public sealed class BotHostedService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly InteractionHandler _interactionHandler;
    private readonly BotConfiguration _config;
    private readonly ILogger<BotHostedService> _logger;

    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BotHostedService(
        DiscordSocketClient client,
        InteractionService interactionService,
        InteractionHandler interactionHandler,
        IOptions<BotConfiguration> config,
        ILogger<BotHostedService> logger)
    {
        _client             = client;
        _interactionService = interactionService;
        _interactionHandler = interactionHandler;
        _config             = config.Value;
        _logger             = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_config.Token))
            throw new InvalidOperationException("DISCORD_TOKEN is not set.");

        _client.Log   += OnLogAsync;
        _client.Ready += OnReadyAsync;

        await _interactionHandler.InitializeAsync();
        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        // Wait up to 30s for the Ready event before continuing
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            await _readyTcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Bot didn't reach Ready state within 30 seconds.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Shutting down Discord bot...");
        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Bot is ready, registering commands...");

        try
        {
            if (_config.GuildId.HasValue)
            {
                await _interactionService.RegisterCommandsToGuildAsync(_config.GuildId.Value);
                _logger.LogInformation("Commands registered to guild {GuildId}", _config.GuildId.Value);
            }
            else
            {
                await _interactionService.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Commands registered globally (may take up to 1 hour to appear).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register slash commands.");
        }

        _readyTcs.TrySetResult();
    }

    private Task OnLogAsync(LogMessage log)
    {
        var level = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error    => LogLevel.Error,
            LogSeverity.Warning  => LogLevel.Warning,
            LogSeverity.Info     => LogLevel.Information,
            LogSeverity.Verbose  => LogLevel.Debug,
            LogSeverity.Debug    => LogLevel.Trace,
            _                    => LogLevel.Information
        };

        if (log.Exception is null)
            _logger.Log(level, "[Discord/{Source}] {Message}", log.Source, log.Message);
        else
            _logger.Log(level, log.Exception, "[Discord/{Source}] {Message}", log.Source, log.Message);

        return Task.CompletedTask;
    }
}
