using DiscordKeyBot.Models;
using System.Text.Json.Serialization;

namespace DiscordKeyBot.API.DTOs;

public sealed record VerifyResponse
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required VerifyStatus Status { get; init; }

    [JsonPropertyName("expires")]
    public DateTime? Expires { get; init; }
}
