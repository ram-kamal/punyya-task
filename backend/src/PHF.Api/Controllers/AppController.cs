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
        var latest = _config["App:LatestVersion"] ?? "1.0.3";
        var notes = _config["App:ReleaseNotes"] ?? "• Classified IDEs and development environments as active (Antigravity, VS Code, Android Studio, Emulators).\n• Total work shift duration accumulation across multiple daily clock-in/out cycles.\n• Enhanced screen reader accessibility and UI optimizations.\n• 100% accurate live attendance, active/idle time tracking, and auto-update support.";
        var downloadUrl = _config["App:DownloadUrl"] ?? "https://punyya.org/task/downloads/PunnyaHeartDesk.exe";
        var fallbackUrl = _config["App:FallbackDownloadUrl"] ?? "https://punnya-api.onrender.com/downloads/PunnyaHeartDesk.exe";

        return Ok(new AppVersionDto
        {
            LatestVersion = latest,
            VersionCode = 103,
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
        var latest = _config["App:LatestVersion"] ?? "1.0.3";
        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = "BROADCAST",
            EventType = "APP_UPDATE",
            Details = $"A new update (v{latest}) is available with active IDE telemetry & shift hours accumulation. Please update."
        });

        return Ok(new { message = $"Update broadcast for version {latest} dispatched successfully." });
    }
}
