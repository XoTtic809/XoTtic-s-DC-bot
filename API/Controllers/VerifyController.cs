using DiscordKeyBot.API.DTOs;
using DiscordKeyBot.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DiscordKeyBot.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("verify_policy")]
public sealed class VerifyController : ControllerBase
{
    private readonly IKeyService _keyService;
    private readonly ILogger<VerifyController> _logger;

    public VerifyController(IKeyService keyService, ILogger<VerifyController> logger)
    {
        _keyService = keyService;
        _logger     = logger;
    }

    // GET /api/verify?key=XXXX-XXXX-XXXX-XXXX&hwid=<fingerprint>
    [HttpGet]
    public async Task<IActionResult> VerifyAsync([FromQuery] string? key, [FromQuery] string? hwid, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "key is required" });

        if (string.IsNullOrWhiteSpace(hwid))
            return BadRequest(new { error = "hwid is required" });

        if (key.Length != 19 || !IsValidKeyFormat(key))
            return BadRequest(new { error = "Invalid key format. Expected XXXX-XXXX-XXXX-XXXX." });

        if (hwid.Length > 512)
            return BadRequest(new { error = "hwid is too long" });

        _logger.LogInformation("Verify: key={Key} ip={IP}", key, HttpContext.Connection.RemoteIpAddress);

        var result = await _keyService.VerifyKeyAsync(key, hwid, ct);

        return Ok(new VerifyResponse
        {
            Status  = result.Status,
            Expires = result.ExpirationDate
        });
    }

    private static bool IsValidKeyFormat(string key)
    {
        var parts = key.ToUpperInvariant().Split('-');
        return parts.Length == 4 && parts.All(p => p.Length == 4 && p.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)));
    }
}
