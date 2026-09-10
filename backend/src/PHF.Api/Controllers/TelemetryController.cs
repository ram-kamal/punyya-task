using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetryService;

    public TelemetryController(ITelemetryService telemetryService)
    {
        _telemetryService = telemetryService;
    }

    [HttpPost("batch")]
    public async Task<IActionResult> IngestBatch([FromBody] TelemetryBatchRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest(new { message = "Invalid telemetry payload." });
        }

        var userClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userClaim) && Guid.TryParse(userClaim, out var parsedGuid))
        {
            var isManagerOrAdmin = User.IsInRole("ADMIN") || User.IsInRole("MANAGER");
            // If standard employee tries to spoof another user ID, reject
            if (!isManagerOrAdmin && request.UserId.HasValue && request.UserId.Value != parsedGuid)
            {
                return Forbid();
            }

            if (!request.UserId.HasValue)
            {
                request = request with { UserId = parsedGuid };
            }
        }

        var ingestedCount = await _telemetryService.IngestBatchAsync(request, cancellationToken);
        return Ok(new
        {
            success = true,
            recordsIngested = ingestedCount,
            receivedAt = DateTime.UtcNow
        });
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] WorkstationHeartbeatDto heartbeat, CancellationToken cancellationToken)
    {
        if (heartbeat == null || string.IsNullOrWhiteSpace(heartbeat.StationId))
        {
            return BadRequest(new { message = "Station ID is required." });
        }

        await _telemetryService.ProcessHeartbeatAsync(heartbeat, cancellationToken);
        return Ok(new { success = true });
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpGet("stations")]
    public async Task<IActionResult> GetStations(CancellationToken cancellationToken)
    {
        var stations = await _telemetryService.GetWorkstationsStatusAsync(cancellationToken);
        return Ok(stations);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpGet("events")]
    public async Task<IActionResult> GetRecentEvents([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var events = await _telemetryService.GetRecentEventsAsync(limit, cancellationToken);
        return Ok(events);
    }
}
