using DiscordKeyBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DiscordKeyBot.Data.Configurations;

public sealed class LicenseKeyConfiguration : IEntityTypeConfiguration<LicenseKey>
{
    public void Configure(EntityTypeBuilder<LicenseKey> builder)
    {
        builder.ToTable("license_keys");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.Id)
               .HasColumnName("id")
               .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(k => k.Key)
               .HasColumnName("key")
               .HasMaxLength(19)
               .IsRequired();

        builder.HasIndex(k => k.Key)
               .IsUnique()
               .HasDatabaseName("ix_license_keys_key");

        builder.Property(k => k.IsUsed)
               .HasColumnName("is_used")
               .HasDefaultValue(false)
               .IsRequired();

        builder.Property(k => k.DiscordUserId)
               .HasColumnName("discord_user_id")
               .HasMaxLength(20)
               .IsRequired(false);

        builder.Property(k => k.HWID)
               .HasColumnName("hwid")
               .HasMaxLength(256)
               .IsRequired(false);

        builder.Property(k => k.ExpirationDate)
               .HasColumnName("expiration_date")
               .IsRequired();

        builder.Property(k => k.CreatedAt)
               .HasColumnName("created_at")
               .HasDefaultValueSql("now()")
               .IsRequired();

        builder.Property(k => k.RedeemedAt)
               .HasColumnName("redeemed_at")
               .IsRequired(false);

        builder.Property(k => k.IsRevoked)
               .HasColumnName("is_revoked")
               .HasDefaultValue(false)
               .IsRequired();

        builder.HasIndex(k => k.DiscordUserId)
               .HasDatabaseName("ix_license_keys_discord_user_id");
    }
}
