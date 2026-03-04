using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace DiscordKeyBot.Bot;

public sealed class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _services;
    private readonly ILogger<InteractionHandler> _logger;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceScopeFactory scopeFactory,
        IServiceProvider services,
        ILogger<InteractionHandler> logger)
    {
        _client             = client;
        _interactionService = interactionService;
        _scopeFactory       = scopeFactory;
        _services           = services;
        _logger             = logger;
    }

    public async Task InitializeAsync()
    {
        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

        _client.InteractionCreated               += HandleInteractionAsync;
        _interactionService.SlashCommandExecuted += OnSlashCommandExecutedAsync;

        _logger.LogInformation("Loaded {Count} command module(s)", _interactionService.Modules.Count);
    }

    private async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        // New scope per interaction so scoped services (DbContext, KeyService) are isolated
        await using var scope = _scopeFactory.CreateAsyncScope();

        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interactionService.ExecuteCommandAsync(ctx, scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling interaction {Id}", interaction.Id);

            if (!interaction.HasResponded)
                await interaction.RespondAsync("Something went wrong. Please contact an admin.", ephemeral: true);
        }
    }

    private Task OnSlashCommandExecutedAsync(SlashCommandInfo info, IInteractionContext context, Discord.Interactions.IResult result)
    {
        if (!result.IsSuccess)
            _logger.LogWarning("/{Command} failed: {Error}", info.Name, result.ErrorReason);

        return Task.CompletedTask;
    }
}
