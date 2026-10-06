using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class TimesheetController : ControllerBase
{
    private readonly ITimesheetService _timesheetService;

    public TimesheetController(ITimesheetService timesheetService)
    {
        _timesheetService = timesheetService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? period,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role)?.ToUpperInvariant();
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        Guid? targetUserId = null;
        if (role == "EMPLOYEE")
        {
            // Employees can strictly only access their own timesheet records
            if (!Guid.TryParse(userIdStr, out var empId))
            {
                return Forbid();
            }
            targetUserId = empId;
        }
        else
        {
            // Admins, Managers, Monitors can view all or filter by employee
            targetUserId = employeeId;
        }

        var start = startDate ?? date;
        var summary = await _timesheetService.GetSummaryAsync(start, endDate, period, targetUserId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMySummary(
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? period,
        CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var empId))
        {
            return Unauthorized();
        }

        var start = startDate ?? date;
        var summary = await _timesheetService.GetSummaryAsync(start, endDate, period, empId, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? startDate, 
        [FromQuery] DateTime? endDate, 
        [FromQuery] string? period,
        [FromQuery] string? viewType,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var role = User.FindFirstValue(ClaimTypes.Role)?.ToUpperInvariant();
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        Guid? targetUserId = null;
        if (role == "EMPLOYEE")
        {
            if (!Guid.TryParse(userIdStr, out var empId))
            {
                return Forbid();
            }
            targetUserId = empId;
        }
        else
        {
            targetUserId = employeeId;
        }

        var bytes = await _timesheetService.ExportTimesheetCsvAsync(startDate, endDate, period, viewType, targetUserId, cancellationToken);
        var filename = $"timesheet_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(bytes, "text/csv", filename);
    }
}
