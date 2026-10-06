using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    private Guid? ResolveTargetEmployee(Guid? requestedEmployeeId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role)?.ToUpperInvariant();
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (role == "EMPLOYEE")
        {
            if (Guid.TryParse(userIdStr, out var empId))
            {
                return empId;
            }
            return Guid.Empty;
        }

        return requestedEmployeeId;
    }

    #region 1. Month-End Attendance Endpoints

    [HttpGet("attendance")]
    public async Task<IActionResult> GetAttendanceReport(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var targetEmp = ResolveTargetEmployee(employeeId);
        var report = await _reportService.GetAttendanceReportAsync(year, month, targetEmp, cancellationToken);
        return Ok(report);
    }

    [HttpGet("attendance/csv")]
    public async Task<IActionResult> ExportAttendanceReportCsv(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var targetEmp = ResolveTargetEmployee(employeeId);
        var bytes = await _reportService.ExportAttendanceReportCsvAsync(year, month, targetEmp, cancellationToken);
        var filename = $"attendance_report_{year ?? DateTime.UtcNow.Year}_{month ?? DateTime.UtcNow.Month:D2}.csv";
        return File(bytes, "text/csv", filename);
    }

    #endregion

    #region 2. Staff Performance Endpoints

    [HttpGet("performance")]
    public async Task<IActionResult> GetPerformanceReport(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? period,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var targetEmp = ResolveTargetEmployee(employeeId);
        var report = await _reportService.GetPerformanceReportAsync(startDate, endDate, period, targetEmp, cancellationToken);
        return Ok(report);
    }

    [HttpGet("performance/csv")]
    public async Task<IActionResult> ExportPerformanceReportCsv(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? period,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var targetEmp = ResolveTargetEmployee(employeeId);
        var bytes = await _reportService.ExportPerformanceReportCsvAsync(startDate, endDate, period, targetEmp, cancellationToken);
        var filename = $"staff_performance_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", filename);
    }

    #endregion

    #region 3. Leave Report Endpoints

    [HttpGet("leaves")]
    public async Task<IActionResult> GetLeaveReport(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? period,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var targetEmp = ResolveTargetEmployee(employeeId);
        var report = await _reportService.GetLeaveReportAsync(startDate, endDate, period, targetEmp, cancellationToken);
        return Ok(report);
    }

    [HttpGet("leaves/csv")]
    public async Task<IActionResult> ExportLeaveReportCsv(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? period,
        [FromQuery] string? viewType,
        [FromQuery] Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var targetEmp = ResolveTargetEmployee(employeeId);
        var bytes = await _reportService.ExportLeaveReportCsvAsync(startDate, endDate, period, viewType, targetEmp, cancellationToken);
        var filename = $"leave_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", filename);
    }

    #endregion
}
