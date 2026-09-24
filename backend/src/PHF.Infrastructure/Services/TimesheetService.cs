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

    public async Task<TimesheetSummaryDto> GetSummaryAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var normalizedPeriod = (period ?? "DAY").Trim().ToUpperInvariant();

        DateTime rangeStart;
        DateTime rangeEnd; // exclusive upper bound

        if (normalizedPeriod == "YESTERDAY")
        {
            rangeStart = now.Date.AddDays(-1);
            rangeEnd = now.Date;
        }
        else if (normalizedPeriod == "WEEK")
        {
            // Start of current week (Monday) to end of current week (Sunday)
            int diff = (7 + (now.Date.DayOfWeek - DayOfWeek.Monday)) % 7;
            rangeStart = now.Date.AddDays(-diff);
            rangeEnd = rangeStart.AddDays(7);
        }
        else if (normalizedPeriod == "MONTH")
        {
            var refDate = startDate ?? now;
            rangeStart = new DateTime(refDate.Year, refDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddMonths(1);
        }
        else if (normalizedPeriod == "LAST_MONTH")
        {
            var firstThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            rangeStart = firstThisMonth.AddMonths(-1);
            rangeEnd = firstThisMonth;
        }
        else if (normalizedPeriod == "YEAR")
        {
            var refDate = startDate ?? now;
            rangeStart = new DateTime(refDate.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddYears(1);
        }
        else if (startDate.HasValue && endDate.HasValue)
        {
            normalizedPeriod = "CUSTOM";
            rangeStart = startDate.Value.Date;
            rangeEnd = endDate.Value.Date.AddDays(1);
        }
        else if (startDate.HasValue)
        {
            normalizedPeriod = "DAY";
            rangeStart = startDate.Value.Date;
            rangeEnd = rangeStart.AddDays(1);
        }
        else
        {
            normalizedPeriod = "TODAY";
            rangeStart = now.Date;
            rangeEnd = rangeStart.AddDays(1);
        }

        if (rangeStart > rangeEnd)
        {
            var temp = rangeStart;
            rangeStart = rangeEnd.AddDays(-1);
            rangeEnd = temp.AddDays(1);
        }

        var stations = await _telemetryService.GetWorkstationsStatusAsync(cancellationToken);
        var activeStationsCount = stations.Count(s => s.State == StationState.ACTIVE.ToString());
        var totalStationsCount = stations.Count;

        var tasksInProgressCount = await _dbContext.Tasks
            .CountAsync(t => t.Status == TaskItemStatus.IN_PROGRESS.ToString(), cancellationToken);

        var shifts = await _dbContext.Shifts
            .Include(s => s.User)
            .Where(s => s.ClockIn >= rangeStart && s.ClockIn < rangeEnd)
            .ToListAsync(cancellationToken);

        var logs = await _dbContext.AppActivityLogs
            .Where(l => l.LoggedAt >= rangeStart && l.LoggedAt < rangeEnd)
            .ToListAsync(cancellationToken);

        var completedTasks = await _dbContext.Tasks
            .Where(t => t.Status == TaskItemStatus.COMPLETED.ToString() && t.CompletedAt >= rangeStart && t.CompletedAt < rangeEnd)
            .ToListAsync(cancellationToken);

        var employees = await _dbContext.Users
            .Where(u => u.Role == UserRole.EMPLOYEE.ToString() 
                     || _dbContext.Shifts.Any(s => s.UserId == u.Id))
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var isSingleDay = (rangeEnd - rangeStart).TotalDays <= 1.0;

        // 1. Build Itemized Daily Breakdown (only on days with shifts)
        var distinctDates = shifts.Select(s => s.ClockIn.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        if (!distinctDates.Any() && isSingleDay)
        {
            distinctDates.Add(rangeStart.Date);
        }

        var dailyRows = new List<TimesheetRowDto>();

        foreach (var day in distinctDates)
        {
            var nextDay = day.AddDays(1);
            var dayShifts = shifts.Where(s => s.ClockIn >= day && s.ClockIn < nextDay).ToList();
            var dayLogs = logs.Where(l => l.LoggedAt >= day && l.LoggedAt < nextDay).ToList();
            var dayTasks = completedTasks.Where(t => t.CompletedAt >= day && t.CompletedAt < nextDay).ToList();

            foreach (var emp in employees)
            {
                var empShifts = dayShifts.Where(s => s.UserId == emp.Id).ToList();

                // An employee must have clocked in for a shift on this day to appear on the timesheet
                if (!empShifts.Any())
                {
                    continue;
                }

                var empLogs = dayLogs.Where(l => l.UserId == emp.Id).ToList();
                var empCompleted = dayTasks.Count(t => t.AssignedTo == emp.Id);

                var loggedActiveSec = empLogs.Where(l => !l.IsIdle).Sum(l => l.DurationSeconds);
                var loggedIdleSec = empLogs.Where(l => l.IsIdle).Sum(l => l.DurationSeconds);

                var shiftTotalSec = 0;
                var shiftActiveSec = 0;
                var shiftIdleSec = 0;

                foreach (var s in empShifts)
                {
                    DateTime sEnd;
                    if (s.Status == ShiftStatus.ACTIVE.ToString())
                    {
                        sEnd = (day == now.Date) ? now : s.ClockIn.Date.AddDays(1);
                    }
                    else
                    {
                        sEnd = s.ClockOut ?? s.ClockIn;
                    }

                    var sElapsed = Math.Max(0, (int)(sEnd - s.ClockIn).TotalSeconds);
                    var sIdle = Math.Min(sElapsed, Math.Max(0, s.TotalIdleSeconds));
                    var sAct = s.TotalActiveSeconds > 0 
                        ? Math.Min(sElapsed - sIdle, s.TotalActiveSeconds) 
                        : Math.Max(0, sElapsed - sIdle);

                    shiftTotalSec += sElapsed;
                    shiftActiveSec += sAct;
                    shiftIdleSec += sIdle;
                }

                int activeSec;
                int idleSec;
                int totalSec = shiftTotalSec;

                var rawIdle = Math.Max(loggedIdleSec, shiftIdleSec);
                var rawActive = Math.Max(loggedActiveSec, shiftActiveSec);

                if (rawActive + rawIdle > 0)
                {
                    if (rawActive + rawIdle > shiftTotalSec)
                    {
                        double activeRatio = (double)rawActive / (rawActive + rawIdle);
                        activeSec = (int)Math.Round(shiftTotalSec * activeRatio);
                        idleSec = Math.Max(0, shiftTotalSec - activeSec);
                    }
                    else
                    {
                        idleSec = Math.Min(shiftTotalSec, rawIdle);
                        activeSec = Math.Max(rawActive, shiftTotalSec - idleSec);
                    }
                }
                else
                {
                    activeSec = shiftTotalSec;
                    idleSec = 0;
                }

                var efficiency = totalSec >= 180 ? Math.Clamp(Math.Round((double)activeSec / totalSec * 100, 1), 0.0, 100.0) : 0.0;
                var firstClockIn = empShifts.OrderBy(s => s.ClockIn).FirstOrDefault()?.ClockIn;
                var lastClockOut = empShifts.OrderByDescending(s => s.ClockOut).FirstOrDefault()?.ClockOut;
                var activeShift = empShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString());

                dailyRows.Add(new TimesheetRowDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    StationId = emp.StationId ?? "—",
                    EmployeeName = emp.FullName,
                    Email = emp.Email,
                    ClockIn = FormatTimeAmPm(firstClockIn),
                    ClockOut = activeShift != null ? "Active" : FormatTimeAmPm(lastClockOut),
                    TotalHours = Math.Round(totalSec / 3600.0, 2),
                    ActiveHours = Math.Round(activeSec / 3600.0, 2),
                    IdleHours = Math.Round(idleSec / 3600.0, 2),
                    TotalSeconds = totalSec,
                    ActiveSeconds = activeSec,
                    IdleSeconds = idleSec,
                    TotalTimeFormatted = FormatHoursMinutes(totalSec),
                    ActiveTimeFormatted = FormatHoursMinutes(activeSec),
                    IdleTimeFormatted = FormatHoursMinutes(idleSec),
                    EfficiencyPercentage = efficiency,
                    TasksCompleted = empCompleted,
                    DaysWorked = totalSec > 0 ? 1 : 0
                });
            }
        }

        // 2. Build Consolidated Rows (Per Employee over the Period)
        var summaryRows = new List<TimesheetRowDto>();

        foreach (var emp in employees)
        {
            var empDailyRows = dailyRows.Where(r => r.Email == emp.Email && r.TotalHours > 0).ToList();
            var daysWorked = empDailyRows.Count;
            if (daysWorked == 0) continue;

            var totalActiveSec = empDailyRows.Sum(r => r.ActiveSeconds);
            var totalIdleSec = empDailyRows.Sum(r => r.IdleSeconds);
            var totalSec = totalActiveSec + totalIdleSec;

            var efficiency = totalSec > 0 
                ? Math.Clamp(Math.Round((double)totalActiveSec / totalSec * 100, 1), 0.0, 100.0) 
                : 0.0;

            var empShifts = shifts.Where(s => s.UserId == emp.Id).ToList();
            var firstClockIn = empShifts.OrderBy(s => s.ClockIn).FirstOrDefault()?.ClockIn;
            var lastClockOut = empShifts.OrderByDescending(s => s.ClockOut).FirstOrDefault()?.ClockOut;
            var activeShift = empShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString());

            var periodLabel = isSingleDay
                ? rangeStart.ToString("yyyy-MM-dd")
                : $"{rangeStart:yyyy-MM-dd} to {rangeEnd.AddDays(-1):yyyy-MM-dd}";

            summaryRows.Add(new TimesheetRowDto
            {
                Date = periodLabel,
                StationId = emp.StationId ?? "—",
                EmployeeName = emp.FullName,
                Email = emp.Email,
                ClockIn = isSingleDay ? FormatTimeAmPm(firstClockIn) : "—",
                ClockOut = isSingleDay ? (activeShift != null ? "Active" : FormatTimeAmPm(lastClockOut)) : "—",
                TotalHours = Math.Round(totalSec / 3600.0, 2),
                ActiveHours = Math.Round(totalActiveSec / 3600.0, 2),
                IdleHours = Math.Round(totalIdleSec / 3600.0, 2),
                TotalSeconds = totalSec,
                ActiveSeconds = totalActiveSec,
                IdleSeconds = totalIdleSec,
                TotalTimeFormatted = FormatHoursMinutes(totalSec),
                ActiveTimeFormatted = FormatHoursMinutes(totalActiveSec),
                IdleTimeFormatted = FormatHoursMinutes(totalIdleSec),
                EfficiencyPercentage = efficiency,
                TasksCompleted = empDailyRows.Sum(r => r.TasksCompleted),
                DaysWorked = daysWorked
            });
        }

        var totalSecAll = isSingleDay ? dailyRows.Sum(r => r.TotalSeconds) : summaryRows.Sum(r => r.TotalSeconds);
        var totalActiveSecAll = isSingleDay ? dailyRows.Sum(r => r.ActiveSeconds) : summaryRows.Sum(r => r.ActiveSeconds);

        var avgEfficiency = totalSecAll > 0 
            ? Math.Clamp(Math.Round((double)totalActiveSecAll / totalSecAll * 100, 1), 0.0, 100.0) 
            : 0.0;

        var totalHoursLogged = Math.Round(totalSecAll / 3600.0, 1);

        return new TimesheetSummaryDto
        {
            Period = normalizedPeriod,
            StartDate = rangeStart.ToString("yyyy-MM-dd"),
            EndDate = rangeEnd.AddDays(-1).ToString("yyyy-MM-dd"),
            ActiveStationsCount = activeStationsCount,
            TotalStationsCount = totalStationsCount,
            TasksInProgressCount = tasksInProgressCount,
            AverageEfficiencyPercentage = avgEfficiency,
            TotalHoursLoggedToday = totalHoursLogged,
            Rows = isSingleDay ? dailyRows : summaryRows,
            DailyBreakdown = dailyRows
        };
    }

    public async Task<byte[]> ExportTimesheetCsvAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null,
        string? viewType = "daily", 
        CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(startDate, endDate, period, cancellationToken);
        var csv = new StringBuilder();

        var isSummaryExport = viewType?.Equals("summary", StringComparison.OrdinalIgnoreCase) == true && summary.Period != "DAY" && summary.Period != "TODAY" && summary.Period != "YESTERDAY";
        var exportRows = isSummaryExport ? summary.Rows : (summary.DailyBreakdown.Any() ? summary.DailyBreakdown : summary.Rows);

        if (isSummaryExport)
        {
            csv.AppendLine("Period,Station,Employee Name,Email,Days Worked,Total Hours,Total Time (HH:MM),Active Hours,Active Time (HH:MM),Idle Hours,Idle Time (HH:MM),Efficiency %,Tasks Completed");
            foreach (var row in exportRows)
            {
                csv.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "\"{0}\",\"{1}\",\"{2}\",\"{3}\",{4},{5:F2},\"{6}\",{7:F2},\"{8}\",{9:F2},\"{10}\",{11:F1}%,{12}",
                    row.Date,
                    row.StationId,
                    row.EmployeeName.Replace("\"", "\"\""),
                    row.Email,
                    row.DaysWorked,
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
        }
        else
        {
            // Standard Daily Timesheet Header with both decimal and human-readable Hours & Minutes
            csv.AppendLine("Date,Station,Employee Name,Email,Clock In,Clock Out,Total Hours,Total Time (HH:MM),Active Hours,Active Time (HH:MM),Idle Hours,Idle Time (HH:MM),Efficiency %,Tasks Completed");
            foreach (var row in exportRows)
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

    private static string FormatTimeAmPm(DateTime? utcTime)
    {
        if (!utcTime.HasValue) return "—";
        // Convert UTC to IST (+5:30)
        var ist = utcTime.Value.AddHours(5.5);
        return ist.ToString("hh:mm:ss tt"); // e.g. "04:03:49 PM"
    }
}
