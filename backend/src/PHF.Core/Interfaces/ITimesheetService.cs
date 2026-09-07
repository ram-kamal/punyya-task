using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface ITimesheetService
{
    Task<TimesheetSummaryDto> GetSummaryAsync(DateTime? date = null, CancellationToken cancellationToken = default);
    Task<byte[]> ExportTimesheetCsvAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
}
