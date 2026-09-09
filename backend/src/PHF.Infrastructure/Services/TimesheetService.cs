using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class TimesheetService : ITimesheetService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITelemetryService _telemetryService;

    public TimesheetService(ApplicationDbContext dbContext, ITelemetryService telemetryService)
    {
        _dbContext = dbContext;
        _telemetryService = telemetryService;
    }

    public async Task<TimesheetSummaryDto> GetSummaryAsync(DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var targetDate = (date ?? DateTime.UtcNow).Date;
        var nextDate = targetDate.AddDays(1);

        var stations = await _telemetryService.GetWorkstationsStatusAsync(cancellationToken);
        var activeStationsCount = stations.Count(s => s.State == StationState.ACTIVE.ToString());
        var totalStationsCount = stations.Count;

        var tasksInProgressCount = await _dbContext.Tasks
            .CountAsync(t => t.Status == TaskItemStatus.IN_PROGRESS.ToString(), cancellationToken);

        var shifts = await _dbContext.Shifts
            .Include(s => s.User)
            .Where(s => s.ClockIn >= targetDate && s.ClockIn < nextDate)
            .ToListAsync(cancellationToken);

        var logs = await _dbContext.AppActivityLogs
            .Where(l => l.LoggedAt >= targetDate && l.LoggedAt < nextDate)
            .ToListAsync(cancellationToken);

        var completedTasks = await _dbContext.Tasks
            .Where(t => t.Status == TaskItemStatus.COMPLETED.ToString() && t.CompletedAt >= targetDate && t.CompletedAt < nextDate)
            .ToListAsync(cancellationToken);

        var rows = new List<TimesheetRowDto>();
        var totalActiveSecondsAll = 0;
        var totalSecondsAll = 0;

        var employees = await _dbContext.Users
            .Where(u => u.Role == UserRole.EMPLOYEE.ToString())
            .ToListAsync(cancellationToken);

        foreach (var emp in employees)
        {
            var empShifts = shifts.Where(s => s.UserId == emp.Id).ToList();
            var empLogs = logs.Where(l => l.UserId == emp.Id).ToList();
            var empCompleted = completedTasks.Count(t => t.AssignedTo == emp.Id);

            var activeSec = empLogs.Where(l => !l.IsIdle).Sum(l => l.DurationSeconds);
            var idleSec = empLogs.Where(l => l.IsIdle).Sum(l => l.DurationSeconds);

            // If shifts had precalculated values
            if (activeSec == 0 && empShifts.Any())
            {
                activeSec = empShifts.Sum(s => s.TotalActiveSeconds);
                idleSec = empShifts.Sum(s => s.TotalIdleSeconds);
            }

            var activeShift = empShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString());
            if (activeShift != null)
            {
                var elapsed = (int)(DateTime.UtcNow - activeShift.ClockIn).TotalSeconds;
                if (elapsed > activeSec + idleSec)
                {
                    activeSec = Math.Max(activeSec, elapsed - idleSec);
                }
            }

            var totalSec = activeSec + idleSec;
            totalActiveSecondsAll += activeSec;
            totalSecondsAll += totalSec;

            var efficiency = totalSec > 0 ? Math.Round((double)activeSec / totalSec * 100, 1) : 100.0;
            var firstClockIn = empShifts.OrderBy(s => s.ClockIn).FirstOrDefault()?.ClockIn;
            var lastClockOut = empShifts.OrderByDescending(s => s.ClockOut).FirstOrDefault()?.ClockOut;

            rows.Add(new TimesheetRowDto
            {
                Date = targetDate.ToString("yyyy-MM-dd"),
                StationId = emp.StationId ?? "—",
                EmployeeName = emp.FullName,
                Email = emp.Email,
                ClockIn = firstClockIn?.ToString("HH:mm:ss") ?? "—",
                ClockOut = lastClockOut?.ToString("HH:mm:ss") ?? (activeShift != null ? "Active" : "—"),
                TotalHours = Math.Round(totalSec / 3600.0, 2),
                ActiveHours = Math.Round(activeSec / 3600.0, 2),
                IdleHours = Math.Round(idleSec / 3600.0, 2),
                TotalTimeFormatted = FormatHoursMinutes(totalSec),
                ActiveTimeFormatted = FormatHoursMinutes(activeSec),
                IdleTimeFormatted = FormatHoursMinutes(idleSec),
                EfficiencyPercentage = efficiency,
                TasksCompleted = empCompleted
            });
        }

        var avgEfficiency = totalSecondsAll > 0 
            ? Math.Round((double)totalActiveSecondsAll / totalSecondsAll * 100, 1) 
            : 92.0;

        var totalHoursLoggedToday = Math.Round(totalSecondsAll / 3600.0, 1);

        return new TimesheetSummaryDto
        {
            ActiveStationsCount = activeStationsCount,
            TotalStationsCount = totalStationsCount,
            TasksInProgressCount = tasksInProgressCount,
            AverageEfficiencyPercentage = avgEfficiency,
            TotalHoursLoggedToday = totalHoursLoggedToday,
            Rows = rows
        };
    }

    public async Task<byte[]> ExportTimesheetCsvAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(startDate, cancellationToken);
        var csv = new StringBuilder();

        // Header with both decimal and human-readable Hours & Minutes
        csv.AppendLine("Date,Station,Employee Name,Email,Clock In,Clock Out,Total Hours,Total Time (HH:MM),Active Hours,Active Time (HH:MM),Idle Hours,Idle Time (HH:MM),Efficiency %,Tasks Completed");

        foreach (var row in summary.Rows)
        {
            csv.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",{6:F2},\"{7}\",{8:F2},\"{9}\",{10:F2},\"{11}\",{12:F1}%,{13}",
                row.Date,
                row.StationId,
                row.EmployeeName.Replace("\"", "\"\""),
                row.Email,
                row.ClockIn,
                row.ClockOut,
                row.TotalHours,
                row.TotalTimeFormatted,
                row.ActiveHours,
                row.ActiveTimeFormatted,
                row.IdleHours,
                row.IdleTimeFormatted,
                row.EfficiencyPercentage,
                row.TasksCompleted
            ));
        }

        // Return with UTF-8 BOM so Excel opens it with proper formatting
        var utf8WithBom = new UTF8Encoding(true);
        return utf8WithBom.GetBytes(csv.ToString());
    }

    private static string FormatHoursMinutes(int totalSeconds)
    {
        if (totalSeconds <= 0) return "0h 0m";
        var totalMinutes = totalSeconds / 60;
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        return $"{h}h {m}m";
    }
}
