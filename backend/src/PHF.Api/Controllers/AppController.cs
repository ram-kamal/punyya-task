using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AppController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public AppController(IConfiguration config, IRealtimeNotifier realtimeNotifier)
    {
        _config = config;
        _realtimeNotifier = realtimeNotifier;
    }

    [AllowAnonymous]
    [HttpGet("version")]
    public IActionResult GetLatestVersion()
    {
        var latest = _config["App:LatestVersion"] ?? "1.0.1";
        var notes = _config["App:ReleaseNotes"] ?? "• Auto clock-out on PC shutdown, restart, and window close.\n• Auto clock-out when PC sleeps or shift exceeds 12 hours.\n• 100% accurate attendance & timesheet reporting.\n• One-click in-app update & restart support.";
        var downloadUrl = _config["App:DownloadUrl"] ?? "https://punnya.org/task/downloads/PunnyaHeartDesk.exe";
        var fallbackUrl = _config["App:FallbackDownloadUrl"] ?? "https://punnya-api.onrender.com/downloads/PunnyaHeartDesk.exe";

        return Ok(new AppVersionDto
        {
            LatestVersion = latest,
            VersionCode = 101,
            ReleaseNotes = notes,
            DownloadUrl = downloadUrl,
            FallbackDownloadUrl = fallbackUrl,
            ReleaseDate = DateTime.UtcNow,
            IsMandatory = false
        });
    }

    [Authorize]
    [HttpPost("broadcast-update")]
    public async Task<IActionResult> BroadcastUpdateNotification(CancellationToken cancellationToken)
    {
        var latest = _config["App:LatestVersion"] ?? "1.0.1";
        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = "BROADCAST",
            EventType = "APP_UPDATE",
            Details = $"A new update (v{latest}) is available with auto-shutdown clock out. Please update."
        });

        return Ok(new { message = $"Update broadcast for version {latest} dispatched successfully." });
    }
}
