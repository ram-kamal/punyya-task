using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class TimesheetService : ITimesheetService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITelemetryService _telemetryService;
    private readonly IShiftService _shiftService;

    public TimesheetService(ApplicationDbContext dbContext, ITelemetryService telemetryService, IShiftService shiftService)
    {
        _dbContext = dbContext;
        _telemetryService = telemetryService;
        _shiftService = shiftService;
    }

    public async Task<TimesheetSummaryDto> GetSummaryAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        // 0. Auto-close any stale active shifts across the system before calculating timesheets
        try
        {
            await _shiftService.AutoCloseStaleShiftsAsync(cancellationToken);
        }
        catch
        {
            // Best effort safeguard
        }

        var now = DateTime.UtcNow;
        var normalizedPeriod = (period ?? "DAY").Trim().ToUpperInvariant();

        DateTime rangeStart;
        DateTime rangeEnd; // exclusive upper bound

        if (normalizedPeriod == "YESTERDAY")
        {
            rangeStart = DateTime.SpecifyKind(now.Date.AddDays(-1), DateTimeKind.Utc);
            rangeEnd = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        }
        else if (normalizedPeriod == "WEEK")
        {
            // Start of current week (Monday) to end of current week (Sunday)
            int diff = (7 + (now.Date.DayOfWeek - DayOfWeek.Monday)) % 7;
            rangeStart = DateTime.SpecifyKind(now.Date.AddDays(-diff), DateTimeKind.Utc);
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
            rangeStart = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            rangeEnd = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
        }
        else if (startDate.HasValue)
        {
            normalizedPeriod = "DAY";
            rangeStart = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddDays(1);
        }
        else
        {
            normalizedPeriod = "TODAY";
            rangeStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddDays(1);
        }

        if (rangeStart > rangeEnd)
        {
            var temp = rangeStart;
            rangeStart = rangeEnd.AddDays(-1);
            rangeEnd = temp.AddDays(1);
        }

        var stations = await _telemetryService.GetWorkstationsStatusAsync(cancellationToken);
        int activeStationsCount;
        int totalStationsCount;

        if (userId.HasValue)
        {
            var userStation = stations.FirstOrDefault(s => s.UserId == userId.Value);
            activeStationsCount = (userStation != null && userStation.State != StationState.OFFLINE.ToString()) ? 1 : 0;
            totalStationsCount = userStation != null ? 1 : 0;
        }
        else
        {
            activeStationsCount = stations.Count(s => s.State != StationState.OFFLINE.ToString());
            totalStationsCount = stations.Count;
        }

        var tasksInProgressQuery = _dbContext.Tasks.Where(t => t.Status == TaskItemStatus.IN_PROGRESS.ToString());
        if (userId.HasValue)
        {
            tasksInProgressQuery = tasksInProgressQuery.Where(t => t.AssignedTo == userId.Value);
        }
        var tasksInProgressCount = await tasksInProgressQuery.CountAsync(cancellationToken);

        var shiftsQuery = _dbContext.Shifts.Include(s => s.User).AsQueryable();
        if (userId.HasValue)
        {
            shiftsQuery = shiftsQuery.Where(s => s.UserId == userId.Value);
        }
        // Shift interval overlap: ClockIn < rangeEnd and (shift is ongoing OR ended at or after rangeStart)
        shiftsQuery = shiftsQuery.Where(s => s.ClockIn < rangeEnd && (s.ClockOut == null || s.ClockOut >= rangeStart));
        var shifts = await shiftsQuery.ToListAsync(cancellationToken);

        var logsQuery = _dbContext.AppActivityLogs.AsQueryable();
        if (userId.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.UserId == userId.Value);
        }
        logsQuery = logsQuery.Where(l => l.LoggedAt >= rangeStart && l.LoggedAt < rangeEnd);

        var logSummaries = await logsQuery
            .GroupBy(l => new { l.UserId, Date = l.LoggedAt.Date, l.IsIdle })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Date,
                g.Key.IsIdle,
                TotalSeconds = g.Sum(l => l.DurationSeconds)
            })
            .ToListAsync(cancellationToken);

        var tasksQuery = _dbContext.Tasks.AsQueryable();
        if (userId.HasValue)
        {
            tasksQuery = tasksQuery.Where(t => t.AssignedTo == userId.Value);
        }
        tasksQuery = tasksQuery.Where(t => t.Status == TaskItemStatus.COMPLETED.ToString() && t.CompletedAt >= rangeStart && t.CompletedAt < rangeEnd);
        var completedTasks = await tasksQuery.ToListAsync(cancellationToken);

        List<User> employees;
        if (userId.HasValue)
        {
            employees = await _dbContext.Users.Where(u => u.Id == userId.Value).ToListAsync(cancellationToken);
        }
        else
        {
            var shiftUserIds = shifts.Select(s => s.UserId).Distinct().ToList();
            employees = await _dbContext.Users
                .Where(u => u.IsActive || shiftUserIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync(cancellationToken);
        }

        var isSingleDay = (rangeEnd - rangeStart).TotalDays <= 1.0;

        // 1. Build Itemized Daily Breakdown
        // Discover every calendar date within [rangeStart.Date, rangeEnd.Date) that has shifts or telemetry activity
        var distinctDatesSet = new HashSet<DateTime>();

        foreach (var s in shifts)
        {
            var sEnd = s.Status == ShiftStatus.ACTIVE.ToString() ? now : (s.ClockOut ?? s.ClockIn);
            if (s.Status == ShiftStatus.ACTIVE.ToString() && (sEnd - s.ClockIn).TotalHours > 12.0)
            {
                sEnd = s.ClockIn.AddHours(12.0);
            }
            var dStart = s.ClockIn.Date < rangeStart.Date ? rangeStart.Date : s.ClockIn.Date;
            var dEnd = sEnd.Date >= rangeEnd.Date ? rangeEnd.AddDays(-1).Date : sEnd.Date;
            for (var cur = dStart; cur <= dEnd; cur = cur.AddDays(1))
            {
                distinctDatesSet.Add(cur);
            }
        }

        foreach (var l in logSummaries)
        {
            if (l.Date >= rangeStart.Date && l.Date < rangeEnd.Date)
            {
                distinctDatesSet.Add(l.Date);
            }
        }

        if (!distinctDatesSet.Any() && isSingleDay)
        {
            distinctDatesSet.Add(rangeStart.Date);
        }

        var distinctDates = distinctDatesSet.OrderByDescending(d => d).ToList();
        var dailyRows = new List<TimesheetRowDto>();

        foreach (var day in distinctDates)
        {
            var dayStart = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);

            // Shifts overlapping this specific calendar day
            var dayShifts = shifts.Where(s =>
            {
                DateTime sEnd;
                if (s.Status == ShiftStatus.ACTIVE.ToString())
                {
                    var maxActive = s.ClockIn.AddHours(12.0);
                    sEnd = now < maxActive ? now : maxActive;
                }
                else
                {
                    sEnd = s.ClockOut ?? s.ClockIn;
                }
                return s.ClockIn < dayEnd && sEnd > dayStart;
            }).ToList();

            var dayLogSummaries = logSummaries.Where(l => l.Date.Date == day.Date).ToList();
            var dayTasks = completedTasks.Where(t => t.CompletedAt >= dayStart && t.CompletedAt < dayEnd).ToList();

            foreach (var emp in employees)
            {
                var empShifts = dayShifts.Where(s => s.UserId == emp.Id).ToList();
                var empLogSummaries = dayLogSummaries.Where(l => l.UserId == emp.Id).ToList();
                var empCompleted = dayTasks.Count(t => t.AssignedTo == emp.Id);

                // An employee must have a shift or logged activity on this day
                if (!empShifts.Any() && !empLogSummaries.Any())
                {
                    continue;
                }

                var loggedActiveSec = empLogSummaries.Where(l => !l.IsIdle).Sum(l => l.TotalSeconds);
                var loggedIdleSec = empLogSummaries.Where(l => l.IsIdle).Sum(l => l.TotalSeconds);

                var shiftTotalSec = 0;
                var shiftActiveSec = 0;
                var shiftIdleSec = 0;

                foreach (var s in empShifts)
                {
                    DateTime sEnd;
                    if (s.Status == ShiftStatus.ACTIVE.ToString())
                    {
                        var maxActive = s.ClockIn.AddHours(12.0);
                        var effectiveNow = now < maxActive ? now : maxActive;
                        sEnd = (dayStart == now.Date) ? effectiveNow : dayEnd;
                    }
                    else
                    {
                        sEnd = s.ClockOut ?? s.ClockIn;
                    }

                    // Strictly clamp the shift to this calendar day [dayStart, dayEnd)
                    var sliceStart = s.ClockIn > dayStart ? s.ClockIn : dayStart;
                    var sliceEnd = sEnd < dayEnd ? sEnd : dayEnd;

                    var sliceElapsed = Math.Max(0, (int)(sliceEnd - sliceStart).TotalSeconds);
                    if (sliceElapsed <= 0) continue;

                    var totalShiftSec = Math.Max(1, (int)(sEnd - s.ClockIn).TotalSeconds);
                    double dayWeight = Math.Clamp((double)sliceElapsed / totalShiftSec, 0.0, 1.0);

                    var sIdle = (int)Math.Round(s.TotalIdleSeconds * dayWeight);
                    sIdle = Math.Clamp(sIdle, 0, sliceElapsed);

                    var sAct = s.TotalActiveSeconds > 0
                        ? (int)Math.Round(s.TotalActiveSeconds * dayWeight)
                        : (sliceElapsed - sIdle);
                    sAct = Math.Clamp(sAct, 0, sliceElapsed - sIdle);

                    shiftTotalSec += sliceElapsed;
                    shiftActiveSec += sAct;
                    shiftIdleSec += sIdle;
                }

                if (shiftTotalSec == 0 && (loggedActiveSec + loggedIdleSec > 0))
                {
                    shiftTotalSec = loggedActiveSec + loggedIdleSec;
                    shiftActiveSec = loggedActiveSec;
                    shiftIdleSec = loggedIdleSec;
                }

                int activeSec;
                int idleSec;
                int totalSec = shiftTotalSec;

                if (loggedActiveSec + loggedIdleSec > 0)
                {
                    if (loggedActiveSec + loggedIdleSec > totalSec)
                    {
                        double activeRatio = (double)loggedActiveSec / (loggedActiveSec + loggedIdleSec);
                        activeSec = (int)Math.Round(totalSec * activeRatio);
                        idleSec = Math.Max(0, totalSec - activeSec);
                    }
                    else
                    {
                        idleSec = Math.Min(totalSec, loggedIdleSec);
                        activeSec = Math.Max(0, totalSec - idleSec);
                    }
                }
                else
                {
                    activeSec = Math.Min(totalSec, shiftActiveSec);
                    idleSec = Math.Max(0, totalSec - activeSec);
                }

                var efficiency = totalSec >= 60 
                    ? Math.Clamp(Math.Round((double)activeSec / totalSec * 100.0, 1), 0.0, 100.0) 
                    : (totalSec > 0 ? 100.0 : 0.0);

                var firstClockIn = empShifts.OrderBy(s => s.ClockIn).FirstOrDefault()?.ClockIn;
                var lastClockOut = empShifts.OrderByDescending(s => s.ClockOut).FirstOrDefault()?.ClockOut;
                var activeShift = empShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString() && (now - s.ClockIn).TotalHours < 12.0);

                dailyRows.Add(new TimesheetRowDto
                {
                    Date = day.ToString("yyyy-MM-dd"),
                    StationId = emp.StationId ?? "—",
                    EmployeeName = emp.FullName,
                    Email = emp.Email,
                    ClockIn = firstClockIn.HasValue ? FormatTimeAmPm(firstClockIn) : "—",
                    ClockOut = activeShift != null ? "Active" : (lastClockOut.HasValue ? FormatTimeAmPm(lastClockOut) : "—"),
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
                ? Math.Clamp(Math.Round((double)totalActiveSec / totalSec * 100.0, 1), 0.0, 100.0) 
                : 0.0;

            var empShifts = shifts.Where(s => s.UserId == emp.Id).ToList();
            var firstClockIn = empShifts.OrderBy(s => s.ClockIn).FirstOrDefault()?.ClockIn;
            var lastClockOut = empShifts.OrderByDescending(s => s.ClockOut).FirstOrDefault()?.ClockOut;
            var activeShift = empShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString() && (now - s.ClockIn).TotalHours < 12.0);

            var periodLabel = isSingleDay
                ? rangeStart.ToString("yyyy-MM-dd")
                : $"{rangeStart:yyyy-MM-dd} to {rangeEnd.AddDays(-1):yyyy-MM-dd}";

            summaryRows.Add(new TimesheetRowDto
            {
                Date = periodLabel,
                StationId = emp.StationId ?? "—",
                EmployeeName = emp.FullName,
                Email = emp.Email,
                ClockIn = isSingleDay ? (firstClockIn.HasValue ? FormatTimeAmPm(firstClockIn) : "—") : "—",
                ClockOut = isSingleDay ? (activeShift != null ? "Active" : (lastClockOut.HasValue ? FormatTimeAmPm(lastClockOut) : "—")) : "—",
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
            ? Math.Clamp(Math.Round((double)totalActiveSecAll / totalSecAll * 100.0, 1), 0.0, 100.0) 
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
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetSummaryAsync(startDate, endDate, period, userId, cancellationToken);
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
