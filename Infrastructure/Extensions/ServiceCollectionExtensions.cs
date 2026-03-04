using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordKeyBot.Bot;
using DiscordKeyBot.Data;
using DiscordKeyBot.Infrastructure.Configuration;
using DiscordKeyBot.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

namespace DiscordKeyBot.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ResolveConnectionString(configuration);

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            });
        });

        return services;
    }

    public static IServiceCollection AddDiscordBot(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BotConfiguration>(options =>
        {
            configuration.GetSection(BotConfiguration.SectionName).Bind(options);

            var token = configuration["DISCORD_TOKEN"];
            if (!string.IsNullOrWhiteSpace(token))
                options.Token = token;

            var logChannel = configuration["LOG_CHANNEL_ID"];
            if (ulong.TryParse(logChannel, out var channelId))
                options.LogChannelId = channelId;
        });

        services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents      = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
            LogLevel            = LogSeverity.Info,
            AlwaysDownloadUsers = false,
            MessageCacheSize    = 0
        }));

        services.AddSingleton<InteractionService>(sp =>
        {
            var client = sp.GetRequiredService<DiscordSocketClient>();
            return new InteractionService(client, new InteractionServiceConfig
            {
                LogLevel          = LogSeverity.Info,
                DefaultRunMode    = RunMode.Sync,
                UseCompiledLambda = true
            });
        });

        services.AddSingleton<InteractionHandler>();
        services.AddSingleton<IDiscordLogService, DiscordLogService>();
        services.AddHostedService<BotHostedService>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IKeyService, KeyService>();
        services.AddSingleton<IKeyGeneratorService, KeyGeneratorService>();
        return services;
    }

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // 30 requests/min per IP on the verify endpoint
            options.AddPolicy("verify_policy", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit          = 30,
                    Window               = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit           = 5
                });
            });

            // Global fallback for all other routes
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit          = 100,
                    Window               = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit           = 10
                });
            });
        });

        return services;
    }

    private static string ResolveConnectionString(IConfiguration configuration)
    {
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return ConvertDatabaseUrl(databaseUrl);

        var connectionString = configuration.GetConnectionString("Default");
        if (!string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        throw new InvalidOperationException(
            "No database connection configured. Set DATABASE_URL or ConnectionStrings:Default.");
    }

    // Railway gives postgresql://user:pass@host:port/db — convert to Npgsql format
    private static string ConvertDatabaseUrl(string databaseUrl)
    {
        var uri      = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);

        if (userInfo.Length != 2)
            throw new InvalidOperationException($"Invalid DATABASE_URL: {databaseUrl}");

        return $"Host={uri.Host};" +
               $"Port={uri.Port};" +
               $"Database={uri.AbsolutePath.TrimStart('/')};" +
               $"Username={Uri.UnescapeDataString(userInfo[0])};" +
               $"Password={Uri.UnescapeDataString(userInfo[1])};" +
               "SSL Mode=Require;Trust Server Certificate=true;";
    }
}
