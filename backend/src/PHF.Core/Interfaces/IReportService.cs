using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface IReportService
{
    Task<MonthEndAttendanceReportDto> GetAttendanceReportAsync(
        int? year = null, 
        int? month = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportAttendanceReportCsvAsync(
        int? year = null, 
        int? month = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default);

    Task<StaffPerformanceReportDto> GetPerformanceReportAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportPerformanceReportCsvAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default);

    Task<LeaveReportDto> GetLeaveReportAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportLeaveReportCsvAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        string? viewType = "summary", 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default);
}
