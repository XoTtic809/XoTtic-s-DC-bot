# Discord Key Bot — Complete Deployment Guide

This document covers everything you need to get the system running end-to-end:
database migrations, Railway deployment, client integration, and expansion paths.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Local Development Setup](#2-local-development-setup)
3. [Database Migration](#3-database-migration)
4. [Discord Application Setup](#4-discord-application-setup)
5. [Deploy to Railway](#5-deploy-to-railway)
6. [Environment Variables Reference](#6-environment-variables-reference)
7. [How the Client Should Call the Verify Endpoint](#7-how-the-client-should-call-the-verify-endpoint)
8. [How to Expand Into a Paid License System](#8-how-to-expand-into-a-paid-license-system)
9. [Security Hardening Checklist](#9-security-hardening-checklist)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 8.0+ | `dotnet --version` to verify |
| PostgreSQL | 14+ | Local dev only; Railway provides managed PG |
| Docker | Any | Required for Railway Docker deployment |
| Railway CLI | Latest | `npm i -g @railway/cli` |
| Git | Any | For pushing to Railway |

---

## 2. Local Development Setup

```bash
# 1. Clone / copy the project folder
cd "Discord Key Bot"

# 2. Copy the example env file
cp .env.example .env

# 3. Fill in .env with your Discord token, local DB URL, and log channel ID

# 4. Restore NuGet packages
dotnet restore

# 5. Apply migrations (creates the database schema)
dotnet ef database update

# 6. Run the application
dotnet run
```

The API will be available at `http://localhost:8080`.
Swagger UI: `http://localhost:8080/swagger` (development only).

---

## 3. Database Migration

### Creating the initial migration

Run this **once**, from inside the project directory:

```bash
dotnet ef migrations add InitialCreate \
    --output-dir Data/Migrations
```

This generates three files in `Data/Migrations/`:
- `<timestamp>_InitialCreate.cs` — the Up/Down migration
- `<timestamp>_InitialCreate.Designer.cs` — EF snapshot metadata
- `AppDbContextModelSnapshot.cs` — current model snapshot

Commit these files to version control. They are safe to commit — they contain no secrets.

### Applying migrations

```bash
# Apply all pending migrations to your local database:
dotnet ef database update

# Apply against a specific connection string (e.g. staging):
dotnet ef database update --connection "Host=...;Database=...;Username=...;Password=..."
```

### In production (Railway)

Migrations are applied **automatically on every startup** via `MigrateAsync()` in Program.cs.
This is safe for a single-instance service. For multi-instance deployments, disable auto-migration
and run `dotnet ef database update` as a Railway "start command" before your app starts.

### Adding a new migration after model changes

```bash
# After modifying a Model or Configuration:
dotnet ef migrations add <MigrationName> --output-dir Data/Migrations
# Then push to Railway — auto-migration runs on next startup
```

---

## 4. Discord Application Setup

### Create the bot

1. Go to [https://discord.com/developers/applications](https://discord.com/developers/applications)
2. Click **New Application** → name it
3. Go to **Bot** tab → click **Add Bot**
4. Copy the **Token** (treat this like a password — never share it)
5. Under **Privileged Gateway Intents**, enable:
   - **Server Members Intent** (needed so the bot can read user roles for admin checks)
6. Under **OAuth2 → URL Generator**:
   - Scopes: `bot` + `applications.commands`
   - Bot Permissions: `Send Messages`, `Embed Links`, `Read Message History`
7. Copy the generated URL and open it in your browser to invite the bot to your server

### Get your Log Channel ID

1. In Discord: **Settings → Advanced → Developer Mode** → turn on
2. Right-click the channel where you want audit logs → **Copy Channel ID**
3. Paste into `LOG_CHANNEL_ID` environment variable

---

## 5. Deploy to Railway

### Option A — Via GitHub (recommended)

```bash
# 1. Create a GitHub repository and push your code
git init
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/yourname/discord-key-bot.git
git push -u origin main

# 2. Go to https://railway.app → New Project → Deploy from GitHub Repo
# 3. Select your repository
# 4. Railway auto-detects the Dockerfile and builds it
```

### Option B — Via Railway CLI

```bash
# 1. Install Railway CLI
npm install -g @railway/cli

# 2. Login
railway login

# 3. Initialize project
railway init

# 4. Deploy
railway up
```

### Add a PostgreSQL database

1. In Railway dashboard → your project → **New** → **Database** → **Add PostgreSQL**
2. Railway automatically sets `DATABASE_URL` in your service's environment
3. No manual connection string needed — the code reads it automatically

### Set Environment Variables

In the Railway dashboard → your service → **Variables** tab:

```
DISCORD_TOKEN        = your_bot_token_here
LOG_CHANNEL_ID       = 123456789012345678
ASPNETCORE_ENVIRONMENT = Production
```

`DATABASE_URL` is set automatically by the Railway PostgreSQL plugin.

### Verify deployment

Once deployed, check:
- `https://your-service.railway.app/health` → should return `{"status":"ok","utc":"..."}`
- Railway **Logs** tab → should show bot connecting and "Slash commands registered globally"
- In Discord, type `/` in your server — the commands should appear after up to 1 hour

---

## 6. Environment Variables Reference

| Variable | Required | Description |
|---|---|---|
| `DISCORD_TOKEN` | **Yes** | Bot token from Discord Developer Portal |
| `DATABASE_URL` | **Yes** | Set automatically by Railway PostgreSQL plugin |
| `LOG_CHANNEL_ID` | **Yes** | Discord channel ID for admin audit logs |
| `ASPNETCORE_ENVIRONMENT` | Recommended | Set to `Production` on Railway |
| `Bot__GuildId` | Optional | Guild ID for instant slash command registration (dev only) |
| `Bot__AdminRoleName` | Optional | Server role name that grants admin commands. Default: `Admin` |
| `ConnectionStrings__Default` | Dev only | Local PostgreSQL connection string |

---

## 7. How the Client Should Call the Verify Endpoint

Your client application (the software users are licensing) should call the verify endpoint
on every startup and periodically while running.

### Endpoint

```
GET https://your-service.railway.app/api/verify?key=XXXX-XXXX-XXXX-XXXX&hwid=<hardware_id>
```

### Response Shape

```json
{
  "status": "VALID",
  "expires": "2025-06-15T12:00:00Z"
}
```

| Status | Meaning | Client Action |
|---|---|---|
| `VALID` | Key is active and HWID matches | Allow the application to run |
| `INVALID` | Key does not exist | Show "invalid key" error, prompt re-entry |
| `EXPIRED` | Key's expiration date has passed | Show "key expired" message |
| `REVOKED` | Key was administratively revoked | Show "key revoked" message |
| `HWID_MISMATCH` | Key is bound to a different machine | Show "wrong machine" error |

### Generating the HWID

The HWID should be a stable, unique fingerprint of the hardware. A common approach:

```csharp
// C# example — combine CPU ID, motherboard serial, disk serial:
using System.Management;
using System.Security.Cryptography;
using System.Text;

public static string GetHWID()
{
    var parts = new List<string>();

    // CPU
    using (var mc = new ManagementClass("Win32_Processor"))
    foreach (ManagementObject mo in mc.GetInstances())
        parts.Add(mo["ProcessorId"]?.ToString() ?? "");

    // Motherboard
    using (var mc = new ManagementClass("Win32_BaseBoard"))
    foreach (ManagementObject mo in mc.GetInstances())
        parts.Add(mo["SerialNumber"]?.ToString() ?? "");

    // Primary disk
    using (var mc = new ManagementClass("Win32_DiskDrive"))
    foreach (ManagementObject mo in mc.GetInstances())
    {
        parts.Add(mo["SerialNumber"]?.ToString() ?? "");
        break; // only first disk
    }

    var raw = string.Join("|", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
    return Convert.ToHexString(hash)[..32]; // 32-char prefix is sufficient
}
```

> **Note:** `System.Management` is Windows-only. For cross-platform, use a library
> like `Hardware.Info` (NuGet) or generate a stable machine-level UUID stored in
> a protected system path.

### C# Client Example

```csharp
public static async Task<bool> VerifyLicenseAsync(string key, HttpClient http)
{
    var hwid = GetHWID();
    var url  = $"https://your-service.railway.app/api/verify?key={Uri.EscapeDataString(key)}&hwid={Uri.EscapeDataString(hwid)}";

    HttpResponseMessage response;
    try
    {
        response = await http.GetAsync(url);
    }
    catch (HttpRequestException)
    {
        // Network failure — decide your offline policy here.
        // For strict enforcement: deny access. For lenient: allow with a grace period.
        return false;
    }

    if (!response.IsSuccessStatusCode)
        return false;

    var json = await response.Content.ReadFromJsonAsync<VerifyResponse>();

    return json?.Status switch
    {
        "VALID"   => true,
        "EXPIRED" => ShowMessage("Your license has expired. Please renew."),
        "REVOKED" => ShowMessage("Your license has been revoked. Contact support."),
        "HWID_MISMATCH" => ShowMessage("This key is registered to a different machine."),
        _ => ShowMessage("Invalid license key.")
    };
}

private static bool ShowMessage(string msg)
{
    Console.WriteLine(msg); // Replace with your UI
    return false;
}

record VerifyResponse(string Status, DateTime? Expires);
```

### Important Security Notes for Clients

1. **Never cache "VALID" responses indefinitely.** Re-verify on startup and every 30–60 minutes.
2. **The key must be stored securely** — at minimum in a protected file, ideally encrypted.
3. **Do not trust the client to decide if the license is valid.** The server decision is final.
4. **Use HTTPS always.** Never call the endpoint over plain HTTP.
5. **Obfuscate your client binary** if license bypass is a concern (e.g., using ConfuserEx for .NET or similar).

---

## 8. How to Expand Into a Paid License System

This section outlines how to evolve this free/manual key system into a full
automated billing and license delivery platform.

### Phase 1: Stripe Integration (Automated Key Delivery)

1. **Add a Stripe webhook endpoint** — POST /api/stripe/webhook
2. On `checkout.session.completed` event:
   - Call `IKeyService.GenerateKeyAsync()` with the purchased duration
   - Email the key to the customer (via SendGrid, Postmark, etc.)
   - Optionally DM the customer on Discord if they linked their account
3. On `customer.subscription.deleted`:
   - Call `IKeyService.RevokeKeyAsync()` to invalidate the key immediately

```csharp
// Skeleton for StripeWebhookController:
[HttpPost("/api/stripe/webhook")]
public async Task<IActionResult> HandleWebhookAsync()
{
    var payload   = await new StreamReader(Request.Body).ReadToEndAsync();
    var signature = Request.Headers["Stripe-Signature"];

    var stripeEvent = EventUtility.ConstructEvent(payload, signature, _stripeWebhookSecret);

    if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted)
    {
        var session = stripeEvent.Data.Object as Session;
        // Generate key based on session.Metadata["key_type"]
        // Send email with key
    }
    return Ok();
}
```

### Phase 2: User Portal

Add a simple Blazor or React front end where customers can:
- Enter their key and see their subscription status
- See expiration date and request renewal
- Submit a HWID reset request (admin-approved)

### Phase 3: HWID Reset Workflow

Currently, once an HWID is bound it is permanent. Add:
- A `HwidResetRequests` table tracking reset requests
- A `/reset-hwid [key]` bot command that creates a pending request
- An admin command `/approve-reset [key]` that clears the HWID
- Or automatic resets after N days with Stripe subscription active

### Phase 4: Multi-Seat Licenses

Add `MaxSeats` and a `LicenseKeyUsage` table:
- Each verify call creates/updates a row keyed by (LicenseKeyId, HWID)
- Count active HWIDs; reject if MaxSeats exceeded
- "Seat" management portal for the license owner

### Phase 5: Offline Grace Period

For client resilience, issue a signed JWT alongside each valid verify response.
The client can verify the JWT signature offline for a configurable grace period
(e.g., 3 days) before requiring an online re-check.

---

## 9. Security Hardening Checklist

- [x] CSPRNG for key generation (no `Random`, no `Guid.NewGuid()` for keys)
- [x] HWID enforced server-side
- [x] Expiration enforced server-side
- [x] Input validation before DB queries
- [x] Global exception handler (no stack traces in API responses)
- [x] Rate limiting (per-IP, built-in .NET 8 middleware)
- [x] Secrets in environment variables (never hardcoded)
- [ ] **Add API key authentication** to `/api/verify` so only your client binary can call it
      (add a shared `X-Api-Key` header check, or issue per-application JWT tokens)
- [ ] **IP allowlist** if your client calls from a predictable IP range (e.g., VPC)
- [ ] **Request signing** — include an HMAC of (key + hwid + timestamp) to prevent replay attacks
- [ ] **Cloudflare proxy** — put the domain behind Cloudflare to absorb DDoS and hide your server IP
- [ ] **Database backups** — enable Railway's automated backups or set up pg_dump via cron
- [ ] **Audit log retention** — periodically archive or truncate the Discord log channel
- [ ] **Penetration test** your verify endpoint before go-live

---

## 10. Troubleshooting

### Bot commands not showing in Discord

Global slash commands take up to 1 hour to propagate. For instant registration during
development, set `Bot__GuildId` to your test server's ID. Check Railway logs for
"Slash commands registered" confirmation.

### `Database migration failed` on startup

1. Check that `DATABASE_URL` is correctly set in Railway Variables.
2. Verify PostgreSQL plugin is running (Railway dashboard → Deployments).
3. Check logs for the specific migration error (schema conflict, permission denied, etc.).

### Bot connects but commands return errors

Enable `ASPNETCORE_ENVIRONMENT=Development` temporarily to see verbose logs.
Check that the bot token has not been regenerated in the Discord developer portal.

### Rate limit responses (HTTP 429)

Increase the limits in `ServiceCollectionExtensions.AddApiRateLimiting()` if your
legitimate client check-in frequency exceeds 30 req/min per IP.

### HWID_MISMATCH after hardware change

Currently there is no self-service HWID reset. An admin must run this SQL directly:

```sql
UPDATE license_keys SET hwid = NULL WHERE key = 'XXXX-XXXX-XXXX-XXXX';
```

Or implement the HWID reset workflow described in section 8.
