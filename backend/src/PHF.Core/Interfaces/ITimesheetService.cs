using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface ITimesheetService
{
    Task<TimesheetSummaryDto> GetSummaryAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportTimesheetCsvAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = null, string? viewType = "daily", Guid? userId = null, CancellationToken cancellationToken = default);
}
