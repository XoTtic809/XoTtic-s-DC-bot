namespace DiscordKeyBot.Models;

public enum VerifyStatus
{
    VALID,
    INVALID,
    HWID_MISMATCH,
    EXPIRED,
    REVOKED
}
