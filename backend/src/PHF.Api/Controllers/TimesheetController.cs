using Microsoft.AspNetCore.Mvc;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

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
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var summary = await _timesheetService.GetSummaryAsync(date, cancellationToken);
        return Ok(summary);
    }

    [HttpGet("export/csv")]
    public async Task<IActionResult> ExportCsv([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate, CancellationToken cancellationToken)
    {
        var bytes = await _timesheetService.ExportTimesheetCsvAsync(startDate, endDate, cancellationToken);
        var filename = $"timesheet_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";

        return File(bytes, "text/csv", filename);
    }
}
