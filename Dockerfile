# ─────────────────────────────────────────────────────────────────────────────
# Multi-stage Dockerfile for Railway deployment.
#
# Stage 1 (build): compiles the .NET 8 project inside the SDK image.
# Stage 2 (runtime): copies only the published output into the smaller
#                    ASP.NET runtime image to minimize the final image size.
#
# Railway detects this Dockerfile automatically. No railway.json is needed
# when using Docker deployment, but we include railway.json anyway for
# configuration options.
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file first and restore dependencies (layer-cached by Docker).
COPY DiscordKeyBot.csproj ./
RUN dotnet restore

# Copy all source files and publish in Release configuration.
COPY . ./
RUN dotnet publish DiscordKeyBot.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:PublishSingleFile=false

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install the EF Core CLI tools for running migrations manually if needed.
# (Migrations are applied automatically on startup via MigrateAsync())
# Remove this if you want a smaller image and rely solely on auto-migration.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Copy published output from build stage.
COPY --from=build /app/publish .

# Railway sets PORT dynamically. ASP.NET Core reads ASPNETCORE_URLS or
# falls back to the Kestrel config in appsettings.json (port 8080).
# Exposing 8080 here is documentation only; Railway maps it automatically.
EXPOSE 8080

# Run as non-root user for security.
RUN adduser --disabled-password --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "DiscordKeyBot.dll"]
