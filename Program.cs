using DiscordKeyBot.API.Middleware;
using DiscordKeyBot.Data;
using DiscordKeyBot.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Railway sets PORT dynamically, so we need to bind to it
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

if (builder.Environment.IsDevelopment())
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
else
    builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddDiscordBot(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Key Bot API",
            Version = "v1"
        });
    });
}

builder.Services.AddApiRateLimiting();

var app = builder.Build();

// Run migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Running database migrations...");
        await db.Database.MigrateAsync();

        // Ensure schema exists regardless of migration state
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS license_keys (
                id                uuid                     NOT NULL DEFAULT gen_random_uuid(),
                key               character varying(19)    NOT NULL,
                is_used           boolean                  NOT NULL DEFAULT false,
                discord_user_id   character varying(20),
                hwid              character varying(256),
                expiration_date   timestamp with time zone NOT NULL,
                created_at        timestamp with time zone NOT NULL DEFAULT now(),
                redeemed_at       timestamp with time zone,
                is_revoked        boolean                  NOT NULL DEFAULT false,
                CONSTRAINT pk_license_keys PRIMARY KEY (id)
            )");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS ix_license_keys_key ON license_keys (key)");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS ix_license_keys_discord_user_id ON license_keys (discord_user_id)");

        logger.LogInformation("Migrations done.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Migration failed, cannot start.");
        throw;
    }
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.Run();
