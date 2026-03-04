using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordKeyBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    key = table.Column<string>(type: "character varying(19)", maxLength: 19, nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    discord_user_id = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    hwid = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    redeemed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_license_keys", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_license_keys_discord_user_id",
                table: "license_keys",
                column: "discord_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_license_keys_key",
                table: "license_keys",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "license_keys");
        }
    }
}
