using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

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

        // Extract user id from token if not in payload
        if (!request.UserId.HasValue)
        {
            var userClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userClaim) && Guid.TryParse(userClaim, out var parsedGuid))
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

    [HttpGet("stations")]
    public async Task<IActionResult> GetStations(CancellationToken cancellationToken)
    {
        var stations = await _telemetryService.GetWorkstationsStatusAsync(cancellationToken);
        return Ok(stations);
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetRecentEvents([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var events = await _telemetryService.GetRecentEventsAsync(limit, cancellationToken);
        return Ok(events);
    }
}
