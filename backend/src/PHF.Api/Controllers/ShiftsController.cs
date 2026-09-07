using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ShiftsController : ControllerBase
{
    private readonly IShiftService _shiftService;

    public ShiftsController(IShiftService shiftService)
    {
        _shiftService = shiftService;
    }

    [Authorize]
    [HttpPost("clock-in")]
    public async Task<IActionResult> ClockIn([FromBody] ShiftClockInRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _shiftService.ClockInAsync(userId, request.StationId, cancellationToken);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("clock-out")]
    public async Task<IActionResult> ClockOut([FromBody] ShiftClockOutRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _shiftService.ClockOutAsync(userId, request.StationId, cancellationToken);
        if (result == null)
        {
            return NotFound(new { message = "No active shift found to clock out." });
        }

        return Ok(result);
    }

    [Authorize]
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentShift(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var shift = await _shiftService.GetCurrentShiftAsync(userId, cancellationToken);
        if (shift == null)
        {
            return Ok(new { status = "NOT_CLOCKED_IN", totalHoursToday = 0.0 });
        }

        return Ok(shift);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var guid))
        {
            return guid;
        }

        return Guid.Parse("a0000000-0000-0000-0000-000000000003"); // fallback st101
    }
}
