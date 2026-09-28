using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[Authorize(Roles = "ADMIN,MANAGER,MONITOR")]
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
        CancellationToken cancellationToken)
    {
        var start = startDate ?? date;
        var summary = await _timesheetService.GetSummaryAsync(start, endDate, period, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? startDate, 
        [FromQuery] DateTime? endDate, 
        [FromQuery] string? period,
        [FromQuery] string? viewType,
        CancellationToken cancellationToken)
    {
        var bytes = await _timesheetService.ExportTimesheetCsvAsync(startDate, endDate, period, viewType, cancellationToken);
        var filename = $"timesheet_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(bytes, "text/csv", filename);
    }
}
