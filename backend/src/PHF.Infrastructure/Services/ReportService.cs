using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IShiftService _shiftService;

    public ReportService(ApplicationDbContext dbContext, IShiftService shiftService)
    {
        _dbContext = dbContext;
        _shiftService = shiftService;
    }

    #region 1. Month-End Attendance Report

    public async Task<MonthEndAttendanceReportDto> GetAttendanceReportAsync(
        int? year = null, 
        int? month = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _shiftService.AutoCloseStaleShiftsAsync(cancellationToken);
        }
        catch { }

        var now = DateTime.UtcNow;
        var targetYear = year ?? now.Year;
        var targetMonth = month ?? now.Month;

        if (targetMonth < 1 || targetMonth > 12) targetMonth = now.Month;
        if (targetYear < 2020 || targetYear > 2050) targetYear = now.Year;

        var monthStart = new DateTime(targetYear, targetMonth, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var totalDaysInMonth = DateTime.DaysInMonth(targetYear, targetMonth);

        // Calculate total working days in month (excluding Sundays)
        int totalWorkingDays = 0;
        int workingDaysElapsed = 0;
        var todayUtcDate = now.Date;

        for (int d = 1; d <= totalDaysInMonth; d++)
        {
            var curDate = new DateTime(targetYear, targetMonth, d, 0, 0, 0, DateTimeKind.Utc);
            if (curDate.DayOfWeek != DayOfWeek.Sunday)
            {
                totalWorkingDays++;
                if (curDate <= todayUtcDate)
                {
                    workingDaysElapsed++;
                }
            }
        }

        // If inspecting a past month, all working days are elapsed
        if (monthEnd <= todayUtcDate)
        {
            workingDaysElapsed = totalWorkingDays;
        }

        // Query Employees
        var empQuery = _dbContext.Users.AsQueryable();
        if (employeeId.HasValue)
        {
            empQuery = empQuery.Where(u => u.Id == employeeId.Value);
        }
        else
        {
            empQuery = empQuery.Where(u => u.IsActive || _dbContext.Shifts.Any(s => s.UserId == u.Id));
        }

        var employees = await empQuery.OrderBy(u => u.FullName).ToListAsync(cancellationToken);

        // Query Shifts overlapping this month
        var shiftsQuery = _dbContext.Shifts
            .Where(s => s.ClockIn < monthEnd && (s.ClockOut == null || s.ClockOut >= monthStart));

        if (employeeId.HasValue)
        {
            shiftsQuery = shiftsQuery.Where(s => s.UserId == employeeId.Value);
        }

        var shifts = await shiftsQuery.ToListAsync(cancellationToken);

        // Query Approved Leaves overlapping this month
        var leavesQuery = _dbContext.LeaveRequests
            .Include(l => l.User)
            .Where(l => l.Status == LeaveStatus.APPROVED.ToString() && l.StartDate < monthEnd && l.EndDate >= monthStart);

        if (employeeId.HasValue)
        {
            leavesQuery = leavesQuery.Where(l => l.UserId == employeeId.Value);
        }

        var approvedLeaves = await leavesQuery.ToListAsync(cancellationToken);

        // Query App Activity Logs for granular active/idle time
        var logsQuery = _dbContext.AppActivityLogs
            .Where(l => l.LoggedAt >= monthStart && l.LoggedAt < monthEnd);

        if (employeeId.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.UserId == employeeId.Value);
        }

        var logSummaries = await logsQuery
            .GroupBy(l => new { l.UserId, Date = l.LoggedAt.Date, l.IsIdle })
            .Select(g => new { g.Key.UserId, g.Key.Date, g.Key.IsIdle, TotalSec = g.Sum(l => l.DurationSeconds) })
            .ToListAsync(cancellationToken);

        var staffSummaries = new List<AttendanceStaffSummaryDto>();
        var dailyLogs = new List<AttendanceDailyLogDto>();

        // Limit daily logs to up to today (or end of month)
        var maxCalendarDay = (monthEnd <= todayUtcDate) ? totalDaysInMonth : Math.Min(totalDaysInMonth, now.Day);

        foreach (var emp in employees)
        {
            var empShifts = shifts.Where(s => s.UserId == emp.Id).ToList();
            var empLeaves = approvedLeaves.Where(l => l.UserId == emp.Id).ToList();
            var empLogs = logSummaries.Where(l => l.UserId == emp.Id).ToList();

            var presentDatesSet = new HashSet<DateTime>();
            int empTotalSec = 0;
            int empActiveSec = 0;
            int empIdleSec = 0;

            // Compute daily shift elapsed time for each day
            for (int dayNum = 1; dayNum <= totalDaysInMonth; dayNum++)
            {
                var dayDate = new DateTime(targetYear, targetMonth, dayNum, 0, 0, 0, DateTimeKind.Utc);
                var nextDay = dayDate.AddDays(1);

                var dayShifts = empShifts.Where(s =>
                {
                    DateTime sEnd;
                    if (s.Status == ShiftStatus.ACTIVE.ToString())
                    {
                        var maxEnd = s.ClockIn.AddHours(12.0);
                        sEnd = now < maxEnd ? now : maxEnd;
                    }
                    else
                    {
                        sEnd = s.ClockOut ?? s.ClockIn;
                    }
                    return s.ClockIn < nextDay && sEnd > dayDate;
                }).ToList();

                var dayLogs = empLogs.Where(l => l.Date.Date == dayDate.Date).ToList();
                var loggedAct = dayLogs.Where(l => !l.IsIdle).Sum(l => l.TotalSec);
                var loggedIdl = dayLogs.Where(l => l.IsIdle).Sum(l => l.TotalSec);

                int dayShiftSec = 0;
                int dayActiveSec = 0;
                int dayIdleSec = 0;

                foreach (var s in dayShifts)
                {
                    DateTime sEnd;
                    if (s.Status == ShiftStatus.ACTIVE.ToString())
                    {
                        var maxEnd = s.ClockIn.AddHours(12.0);
                        var effectiveNow = now < maxEnd ? now : maxEnd;
                        sEnd = (dayDate == now.Date ? effectiveNow : nextDay);
                    }
                    else
                    {
                        sEnd = s.ClockOut ?? s.ClockIn;
                    }

                    var sliceStart = s.ClockIn > dayDate ? s.ClockIn : dayDate;
                    var sliceEnd = sEnd < nextDay ? sEnd : nextDay;

                    var sliceElapsed = Math.Max(0, (int)(sliceEnd - sliceStart).TotalSeconds);
                    if (sliceElapsed <= 0) continue;

                    var totalShiftSec = Math.Max(1, (int)(sEnd - s.ClockIn).TotalSeconds);
                    double weight = Math.Clamp((double)sliceElapsed / totalShiftSec, 0.0, 1.0);

                    var sIdle = (int)Math.Round(s.TotalIdleSeconds * weight);
                    sIdle = Math.Clamp(sIdle, 0, sliceElapsed);
                    var sAct = s.TotalActiveSeconds > 0
                        ? (int)Math.Round(s.TotalActiveSeconds * weight)
                        : (sliceElapsed - sIdle);
                    sAct = Math.Clamp(sAct, 0, sliceElapsed - sIdle);

                    dayShiftSec += sliceElapsed;
                    dayActiveSec += sAct;
                    dayIdleSec += sIdle;
                }

                if (dayShiftSec == 0 && (loggedAct + loggedIdl > 0))
                {
                    dayShiftSec = loggedAct + loggedIdl;
                    dayActiveSec = loggedAct;
                    dayIdleSec = loggedIdl;
                }

                int dailySec = dayShiftSec;
                int dailyAct;
                int dailyIdl;

                if (loggedAct + loggedIdl > 0)
                {
                    if (loggedAct + loggedIdl > dailySec)
                    {
                        double ratio = (double)loggedAct / (loggedAct + loggedIdl);
                        dailyAct = (int)Math.Round(dailySec * ratio);
                        dailyIdl = Math.Max(0, dailySec - dailyAct);
                    }
                    else
                    {
                        dailyIdl = Math.Min(dailySec, loggedIdl);
                        dailyAct = Math.Max(0, dailySec - dailyIdl);
                    }
                }
                else
                {
                    dailyAct = Math.Min(dailySec, dayActiveSec);
                    dailyIdl = Math.Max(0, dailySec - dailyAct);
                }

                if (dailySec > 0 || dayShifts.Any())
                {
                    presentDatesSet.Add(dayDate.Date);
                    empTotalSec += dailySec;
                    empActiveSec += dailyAct;
                    empIdleSec += dailyIdl;
                }

                // Build daily calendar record (up to maxCalendarDay)
                if (dayNum <= maxCalendarDay)
                {
                    var isWeekend = dayDate.DayOfWeek == DayOfWeek.Sunday;
                    var isLeave = empLeaves.Any(l => dayDate.Date >= l.StartDate.Date && dayDate.Date <= l.EndDate.Date);
                    var isPresent = presentDatesSet.Contains(dayDate.Date);

                    string status;
                    if (isPresent) status = "PRESENT";
                    else if (isLeave) status = "LEAVE";
                    else if (isWeekend) status = "WEEKEND";
                    else status = "ABSENT";

                    var firstIn = dayShifts.OrderBy(s => s.ClockIn).FirstOrDefault()?.ClockIn;
                    var lastOut = dayShifts.OrderByDescending(s => s.ClockOut).FirstOrDefault()?.ClockOut;
                    var activeShift = dayShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString());

                    dailyLogs.Add(new AttendanceDailyLogDto
                    {
                        Date = dayDate.ToString("yyyy-MM-dd"),
                        DayOfWeek = dayDate.DayOfWeek.ToString(),
                        UserId = emp.Id,
                        EmployeeName = emp.FullName,
                        Email = emp.Email,
                        StationId = emp.StationId ?? "—",
                        Status = status,
                        ClockIn = firstIn.HasValue ? FormatTimeAmPm(firstIn) : "—",
                        ClockOut = activeShift != null ? "Active" : (lastOut.HasValue ? FormatTimeAmPm(lastOut) : "—"),
                        HoursWorked = Math.Round(dailySec / 3600.0, 2),
                        TimeFormatted = FormatHoursMinutes(dailySec)
                    });
                }
            }

            // Calculate employee's approved leave days in this month
            int leaveDaysCount = 0;
            for (int d = 1; d <= totalDaysInMonth; d++)
            {
                var curDate = new DateTime(targetYear, targetMonth, d, 0, 0, 0, DateTimeKind.Utc);
                if (curDate.DayOfWeek != DayOfWeek.Sunday && curDate <= todayUtcDate)
                {
                    if (empLeaves.Any(l => curDate.Date >= l.StartDate.Date && curDate.Date <= l.EndDate.Date))
                    {
                        leaveDaysCount++;
                    }
                }
            }

            int daysPresent = presentDatesSet.Count;
            int daysAbsent = Math.Max(0, workingDaysElapsed - daysPresent - leaveDaysCount);
            double attendanceRate = workingDaysElapsed > 0 
                ? Math.Clamp(Math.Round((double)daysPresent / workingDaysElapsed * 100.0, 1), 0.0, 100.0) 
                : 100.0;

            double efficiency = empTotalSec > 0 
                ? Math.Clamp(Math.Round((double)empActiveSec / empTotalSec * 100.0, 1), 0.0, 100.0) 
                : 0.0;

            double totalHours = Math.Round(empTotalSec / 3600.0, 2);
            double activeHours = Math.Round(empActiveSec / 3600.0, 2);
            double idleHours = Math.Round(empIdleSec / 3600.0, 2);
            double avgDailyHours = daysPresent > 0 ? Math.Round(totalHours / daysPresent, 2) : 0.0;

            staffSummaries.Add(new AttendanceStaffSummaryDto
            {
                UserId = emp.Id,
                EmployeeName = emp.FullName,
                Email = emp.Email,
                StationId = emp.StationId ?? "—",
                WorkMode = emp.WorkMode ?? "REMOTE_WFH",
                TotalWorkingDays = totalWorkingDays,
                DaysPresent = daysPresent,
                DaysAbsent = daysAbsent,
                ApprovedLeaveDays = leaveDaysCount,
                AttendanceRatePercentage = attendanceRate,
                TotalHours = totalHours,
                TotalTimeFormatted = FormatHoursMinutes(empTotalSec),
                ActiveHours = activeHours,
                IdleHours = idleHours,
                AverageDailyHours = avgDailyHours,
                EfficiencyPercentage = efficiency
            });
        }

        var periodLabel = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        var avgTeamAttendance = staffSummaries.Any() 
            ? Math.Round(staffSummaries.Average(s => s.AttendanceRatePercentage), 1) 
            : 0.0;

        var totalHoursLoggedAll = Math.Round(staffSummaries.Sum(s => s.TotalHours), 1);

        return new MonthEndAttendanceReportDto
        {
            Period = periodLabel,
            Year = targetYear,
            Month = targetMonth,
            StartDate = monthStart.ToString("yyyy-MM-dd"),
            EndDate = monthEnd.AddDays(-1).ToString("yyyy-MM-dd"),
            TotalCalendarDays = totalDaysInMonth,
            TotalWorkingDays = totalWorkingDays,
            WorkingDaysElapsed = workingDaysElapsed,
            AverageAttendanceRate = avgTeamAttendance,
            TotalHoursLogged = totalHoursLoggedAll,
            StaffSummaries = staffSummaries,
            DailyAttendanceLogs = dailyLogs
        };
    }

    public async Task<byte[]> ExportAttendanceReportCsvAsync(
        int? year = null, 
        int? month = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default)
    {
        var report = await GetAttendanceReportAsync(year, month, employeeId, cancellationToken);
        var sb = new StringBuilder();

        // Month-End Attendance Executive Header
        sb.AppendLine($"# PUNNYA HEART FOUNDATION - MONTH-END ATTENDANCE REPORT");
        sb.AppendLine($"# Period: {report.Period} ({report.StartDate} to {report.EndDate})");
        sb.AppendLine($"# Total Working Days: {report.TotalWorkingDays} | Elapsed: {report.WorkingDaysElapsed} | Team Attendance Rate: {report.AverageAttendanceRate}% | Total Hours Logged: {report.TotalHoursLogged}h");
        sb.AppendLine();

        sb.AppendLine("Employee Name,Email,Station ID,Work Mode,Working Days,Days Present,Days Absent,Approved Leave Days,Attendance Rate %,Total Hours,Total Time (HH:MM),Active Hours,Idle Hours,Avg Daily Hours,Efficiency %");

        foreach (var s in report.StaffSummaries)
        {
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",\"{2}\",\"{3}\",{4},{5},{6},{7},{8:F1}%,{9:F2},\"{10}\",{11:F2},{12:F2},{13:F2},{14:F1}%",
                s.EmployeeName.Replace("\"", "\"\""),
                s.Email,
                s.StationId,
                s.WorkMode,
                s.TotalWorkingDays,
                s.DaysPresent,
                s.DaysAbsent,
                s.ApprovedLeaveDays,
                s.AttendanceRatePercentage,
                s.TotalHours,
                s.TotalTimeFormatted,
                s.ActiveHours,
                s.IdleHours,
                s.AverageDailyHours,
                s.EfficiencyPercentage
            ));
        }

        sb.AppendLine();
        sb.AppendLine("# DAILY ATTENDANCE AUDIT LOG");
        sb.AppendLine("Date,Day of Week,Employee Name,Station ID,Status,Clock In,Clock Out,Hours Worked,Time (HH:MM)");

        foreach (var d in report.DailyAttendanceLogs.OrderByDescending(x => x.Date).ThenBy(x => x.EmployeeName))
        {
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",\"{6}\",{7:F2},\"{8}\"",
                d.Date,
                d.DayOfWeek,
                d.EmployeeName.Replace("\"", "\"\""),
                d.StationId,
                d.Status,
                d.ClockIn,
                d.ClockOut,
                d.HoursWorked,
                d.TimeFormatted
            ));
        }

        var utf8WithBom = new UTF8Encoding(true);
        return utf8WithBom.GetBytes(sb.ToString());
    }

    #endregion

    #region 2. Staff Performance Report

    public async Task<StaffPerformanceReportDto> GetPerformanceReportAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _shiftService.AutoCloseStaleShiftsAsync(cancellationToken);
        }
        catch { }

        var (rangeStart, rangeEnd, periodLabel) = ResolveDateRange(startDate, endDate, period);

        // Query Employees
        var empQuery = _dbContext.Users.AsQueryable();
        if (employeeId.HasValue)
        {
            empQuery = empQuery.Where(u => u.Id == employeeId.Value);
        }
        else
        {
            empQuery = empQuery.Where(u => u.IsActive || _dbContext.Tasks.Any(t => t.AssignedTo == u.Id));
        }

        var employees = await empQuery.OrderBy(u => u.FullName).ToListAsync(cancellationToken);

        // Query Tasks active or completed in range
        var taskQuery = _dbContext.Tasks.Include(t => t.Attachments).AsQueryable();
        if (employeeId.HasValue)
        {
            taskQuery = taskQuery.Where(t => t.AssignedTo == employeeId.Value);
        }

        // Active in period or created in period or completed in period
        taskQuery = taskQuery.Where(t => t.CreatedAt < rangeEnd && (t.CompletedAt == null || t.CompletedAt >= rangeStart));
        var tasks = await taskQuery.ToListAsync(cancellationToken);

        // Query Shifts overlapping period
        var shiftsQuery = _dbContext.Shifts
            .Where(s => s.ClockIn < rangeEnd && (s.ClockOut == null || s.ClockOut >= rangeStart));

        if (employeeId.HasValue)
        {
            shiftsQuery = shiftsQuery.Where(s => s.UserId == employeeId.Value);
        }

        var shifts = await shiftsQuery.ToListAsync(cancellationToken);

        // Query Logs for active vs idle
        var logsQuery = _dbContext.AppActivityLogs
            .Where(l => l.LoggedAt >= rangeStart && l.LoggedAt < rangeEnd);

        if (employeeId.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.UserId == employeeId.Value);
        }

        var logSummaries = await logsQuery
            .GroupBy(l => new { l.UserId, l.IsIdle })
            .Select(g => new { g.Key.UserId, g.Key.IsIdle, TotalSec = g.Sum(l => l.DurationSeconds) })
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var performanceRows = new List<StaffPerformanceRowDto>();

        foreach (var emp in employees)
        {
            var empTasks = tasks.Where(t => t.AssignedTo == emp.Id).ToList();
            var empShifts = shifts.Where(s => s.UserId == emp.Id).ToList();
            var empLogs = logSummaries.Where(l => l.UserId == emp.Id).ToList();

            var assignedCount = empTasks.Count;
            var completedCount = empTasks.Count(t => t.Status == TaskItemStatus.COMPLETED.ToString() && t.CompletedAt >= rangeStart && t.CompletedAt < rangeEnd);
            var inProgressCount = empTasks.Count(t => t.Status == TaskItemStatus.IN_PROGRESS.ToString());
            var pendingCount = empTasks.Count(t => t.Status == TaskItemStatus.ASSIGNED.ToString() || t.Status == TaskItemStatus.UNDER_REVIEW.ToString());

            var completionRate = assignedCount > 0 
                ? Math.Clamp(Math.Round((double)completedCount / assignedCount * 100.0, 1), 0.0, 100.0) 
                : 0.0;

            // Deliverables tracking for completed deliverables
            var deliverablesReq = empTasks.Count(t => t.DeliverableRequired && t.Status == TaskItemStatus.COMPLETED.ToString());
            var deliverablesSub = empTasks.Count(t => t.DeliverableRequired && t.Status == TaskItemStatus.COMPLETED.ToString() && (!string.IsNullOrEmpty(t.DeliverableUrl) || !string.IsNullOrEmpty(t.DeliverableFileName) || (t.Attachments != null && t.Attachments.Any())));
            var deliverableCompliance = deliverablesReq > 0 
                ? Math.Clamp(Math.Round((double)deliverablesSub / deliverablesReq * 100.0, 1), 0.0, 100.0) 
                : 100.0;

            // Calculate shift hours strictly bounded to [rangeStart, rangeEnd)
            int shiftTotalSec = 0;
            int shiftActSec = 0;
            int shiftIdlSec = 0;

            foreach (var s in empShifts)
            {
                DateTime sEnd;
                if (s.Status == ShiftStatus.ACTIVE.ToString())
                {
                    var maxEnd = s.ClockIn.AddHours(12.0);
                    sEnd = now < maxEnd ? now : maxEnd;
                }
                else
                {
                    sEnd = s.ClockOut ?? s.ClockIn;
                }
                var sliceStart = s.ClockIn > rangeStart ? s.ClockIn : rangeStart;
                var sliceEnd = sEnd < rangeEnd ? sEnd : rangeEnd;
                var elapsed = Math.Max(0, (int)(sliceEnd - sliceStart).TotalSeconds);
                if (elapsed <= 0) continue;

                var totalShiftSec = Math.Max(1, (int)(sEnd - s.ClockIn).TotalSeconds);
                double weight = Math.Clamp((double)elapsed / totalShiftSec, 0.0, 1.0);

                var sIdle = (int)Math.Round(s.TotalIdleSeconds * weight);
                sIdle = Math.Clamp(sIdle, 0, elapsed);
                var sAct = s.TotalActiveSeconds > 0 ? (int)Math.Round(s.TotalActiveSeconds * weight) : (elapsed - sIdle);
                sAct = Math.Clamp(sAct, 0, elapsed - sIdle);

                shiftTotalSec += elapsed;
                shiftActSec += sAct;
                shiftIdlSec += sIdle;
            }

            var loggedAct = empLogs.FirstOrDefault(l => !l.IsIdle)?.TotalSec ?? 0;
            var loggedIdl = empLogs.FirstOrDefault(l => l.IsIdle)?.TotalSec ?? 0;

            if (shiftTotalSec == 0 && (loggedAct + loggedIdl > 0))
            {
                shiftTotalSec = loggedAct + loggedIdl;
                shiftActSec = loggedAct;
                shiftIdlSec = loggedIdl;
            }

            int finalSec = shiftTotalSec;
            int finalAct;
            int finalIdl;

            if (loggedAct + loggedIdl > 0)
            {
                if (loggedAct + loggedIdl > finalSec)
                {
                    double ratio = (double)loggedAct / (loggedAct + loggedIdl);
                    finalAct = (int)Math.Round(finalSec * ratio);
                    finalIdl = Math.Max(0, finalSec - finalAct);
                }
                else
                {
                    finalIdl = Math.Min(finalSec, loggedIdl);
                    finalAct = Math.Max(0, finalSec - finalIdl);
                }
            }
            else
            {
                finalAct = Math.Min(finalSec, shiftActSec);
                finalIdl = Math.Max(0, finalSec - finalAct);
            }

            double focusEfficiency = finalSec >= 60 
                ? Math.Clamp(Math.Round((double)finalAct / finalSec * 100.0, 1), 0.0, 100.0) 
                : (finalSec > 0 ? 100.0 : 0.0);

            // Compute Performance Rating
            double combinedScore = (completionRate * 0.5) + (focusEfficiency * 0.5);
            string rating;
            if (assignedCount == 0 && finalSec > 0)
            {
                rating = focusEfficiency >= 85.0 ? "High Performer" : (focusEfficiency >= 65.0 ? "Good Standing" : "Needs Attention");
            }
            else if (combinedScore >= 88.0)
            {
                rating = "Outstanding (90%+)";
            }
            else if (combinedScore >= 75.0)
            {
                rating = "High Performer";
            }
            else if (combinedScore >= 55.0)
            {
                rating = "Good Standing";
            }
            else
            {
                rating = "Needs Attention";
            }

            performanceRows.Add(new StaffPerformanceRowDto
            {
                UserId = emp.Id,
                EmployeeName = emp.FullName,
                Email = emp.Email,
                Role = emp.Role,
                StationId = emp.StationId ?? "—",
                TasksAssigned = assignedCount,
                TasksCompleted = completedCount,
                TasksInProgress = inProgressCount,
                TasksPending = pendingCount,
                TaskCompletionRate = completionRate,
                TotalHoursWorked = Math.Round(finalSec / 3600.0, 2),
                TotalTimeFormatted = FormatHoursMinutes(finalSec),
                ActiveHours = Math.Round(finalAct / 3600.0, 2),
                IdleHours = Math.Round(finalIdl / 3600.0, 2),
                FocusEfficiencyPercentage = focusEfficiency,
                DeliverablesSubmitted = deliverablesSub,
                DeliverablesRequired = deliverablesReq,
                DeliverableComplianceRate = deliverableCompliance,
                PerformanceRating = rating
            });
        }

        var totalAssignedAll = performanceRows.Sum(p => p.TasksAssigned);
        var totalCompletedAll = performanceRows.Sum(p => p.TasksCompleted);
        var avgCompletionRate = totalAssignedAll > 0 
            ? Math.Round((double)totalCompletedAll / totalAssignedAll * 100.0, 1) 
            : 0.0;

        var avgFocus = performanceRows.Any(p => p.TotalHoursWorked > 0)
            ? Math.Round(performanceRows.Where(p => p.TotalHoursWorked > 0).Average(p => p.FocusEfficiencyPercentage), 1)
            : 0.0;

        return new StaffPerformanceReportDto
        {
            Period = periodLabel,
            StartDate = rangeStart.ToString("yyyy-MM-dd"),
            EndDate = rangeEnd.AddDays(-1).ToString("yyyy-MM-dd"),
            TotalTasksAssigned = totalAssignedAll,
            TotalTasksCompleted = totalCompletedAll,
            TeamCompletionRate = avgCompletionRate,
            AverageFocusScore = avgFocus,
            StaffPerformance = performanceRows
        };
    }

    public async Task<byte[]> ExportPerformanceReportCsvAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default)
    {
        var report = await GetPerformanceReportAsync(startDate, endDate, period, employeeId, cancellationToken);
        var sb = new StringBuilder();

        sb.AppendLine($"# PUNNYA HEART FOUNDATION - STAFF PERFORMANCE & VELOCITY REPORT");
        sb.AppendLine($"# Period: {report.Period} ({report.StartDate} to {report.EndDate})");
        sb.AppendLine($"# Total Tasks Assigned: {report.TotalTasksAssigned} | Completed: {report.TotalTasksCompleted} | Team Velocity: {report.TeamCompletionRate}% | Avg Focus Score: {report.AverageFocusScore}%");
        sb.AppendLine();

        sb.AppendLine("Employee Name,Email,Role,Station ID,Tasks Assigned,Tasks Completed,In Progress,Pending,Completion Rate %,Hours Worked,Total Time (HH:MM),Active Hours,Idle Hours,Focus Score %,Deliverables Submitted,Deliverables Required,Deliverable Compliance %,Performance Rating");

        foreach (var p in report.StaffPerformance)
        {
            sb.AppendLine(string.Format(
                CultureInfo.InvariantCulture,
                "\"{0}\",\"{1}\",\"{2}\",\"{3}\",{4},{5},{6},{7},{8:F1}%,{9:F2},\"{10}\",{11:F2},{12:F2},{13:F1}%,{14},{15},{16:F1}%,\"{17}\"",
                p.EmployeeName.Replace("\"", "\"\""),
                p.Email,
                p.Role,
                p.StationId,
                p.TasksAssigned,
                p.TasksCompleted,
                p.TasksInProgress,
                p.TasksPending,
                p.TaskCompletionRate,
                p.TotalHoursWorked,
                p.TotalTimeFormatted,
                p.ActiveHours,
                p.IdleHours,
                p.FocusEfficiencyPercentage,
                p.DeliverablesSubmitted,
                p.DeliverablesRequired,
                p.DeliverableComplianceRate,
                p.PerformanceRating
            ));
        }

        var utf8WithBom = new UTF8Encoding(true);
        return utf8WithBom.GetBytes(sb.ToString());
    }

    #endregion

    #region 3. Leave Report

    public async Task<LeaveReportDto> GetLeaveReportAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default)
    {
        var (rangeStart, rangeEnd, periodLabel) = ResolveDateRange(startDate, endDate, period);

        // Query Leaves overlapping range
        var leavesQuery = _dbContext.LeaveRequests
            .Include(l => l.User)
            .Include(l => l.Reviewer)
            .Where(l => l.StartDate < rangeEnd && l.EndDate >= rangeStart);

        if (employeeId.HasValue)
        {
            leavesQuery = leavesQuery.Where(l => l.UserId == employeeId.Value);
        }

        var leaves = await leavesQuery.OrderByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);

        // Query Employees
        var empQuery = _dbContext.Users.AsQueryable();
        if (employeeId.HasValue)
        {
            empQuery = empQuery.Where(u => u.Id == employeeId.Value);
        }
        else
        {
            empQuery = empQuery.Where(u => u.IsActive || _dbContext.LeaveRequests.Any(l => l.UserId == u.Id));
        }

        var employees = await empQuery.OrderBy(u => u.FullName).ToListAsync(cancellationToken);

        var staffSummaries = new List<LeaveStaffSummaryDto>();

        foreach (var emp in employees)
        {
            var empLeaves = leaves.Where(l => l.UserId == emp.Id).ToList();
            var approved = empLeaves.Where(l => l.Status == LeaveStatus.APPROVED.ToString()).ToList();
            var pending = empLeaves.Where(l => l.Status == LeaveStatus.PENDING.ToString()).ToList();
            var rejected = empLeaves.Where(l => l.Status == LeaveStatus.REJECTED.ToString()).ToList();

            int approvedDays = approved.Sum(l => l.TotalDays);
            int casualDays = approved.Where(l => l.LeaveType == LeaveType.CASUAL.ToString()).Sum(l => l.TotalDays);
            int sickDays = approved.Where(l => l.LeaveType == LeaveType.SICK.ToString()).Sum(l => l.TotalDays);
            int medicalDays = approved.Where(l => l.LeaveType == LeaveType.MEDICAL.ToString()).Sum(l => l.TotalDays);
            int annualDays = approved.Where(l => l.LeaveType == LeaveType.ANNUAL.ToString()).Sum(l => l.TotalDays);
            int emergencyDays = approved.Where(l => l.LeaveType == LeaveType.EMERGENCY.ToString()).Sum(l => l.TotalDays);

            staffSummaries.Add(new LeaveStaffSummaryDto
            {
                UserId = emp.Id,
                EmployeeName = emp.FullName,
                Email = emp.Email,
                StationId = emp.StationId ?? "—",
                TotalRequests = empLeaves.Count,
                TotalApprovedDays = approvedDays,
                CasualLeaveDays = casualDays,
                SickLeaveDays = sickDays,
                MedicalLeaveDays = medicalDays,
                AnnualLeaveDays = annualDays,
                EmergencyLeaveDays = emergencyDays,
                PendingRequests = pending.Count,
                RejectedRequests = rejected.Count
            });
        }

        var detailedRecords = leaves.Select(l => new LeaveDetailedRowDto
        {
            Id = l.Id,
            UserId = l.UserId,
            EmployeeName = l.User?.FullName ?? "Unknown",
            Email = l.User?.Email ?? "—",
            LeaveType = l.LeaveType,
            StartDate = l.StartDate.ToString("yyyy-MM-dd"),
            EndDate = l.EndDate.ToString("yyyy-MM-dd"),
            TotalDays = l.TotalDays,
            Reason = l.Reason,
            Status = l.Status,
            ReviewerName = l.Reviewer?.FullName ?? "—",
            ReviewComments = l.ReviewComments ?? "—",
            AppliedAt = l.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            ReviewedAt = l.ReviewedAt?.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        var totalApprovedDaysAll = staffSummaries.Sum(s => s.TotalApprovedDays);
        var totalPendingAll = staffSummaries.Sum(s => s.PendingRequests);
        var totalRejectedAll = staffSummaries.Sum(s => s.RejectedRequests);

        return new LeaveReportDto
        {
            Period = periodLabel,
            StartDate = rangeStart.ToString("yyyy-MM-dd"),
            EndDate = rangeEnd.AddDays(-1).ToString("yyyy-MM-dd"),
            TotalApplications = leaves.Count,
            TotalApprovedDays = totalApprovedDaysAll,
            TotalPendingRequests = totalPendingAll,
            TotalRejectedRequests = totalRejectedAll,
            StaffSummaries = staffSummaries,
            DetailedRecords = detailedRecords
        };
    }

    public async Task<byte[]> ExportLeaveReportCsvAsync(
        DateTime? startDate = null, 
        DateTime? endDate = null, 
        string? period = null, 
        string? viewType = "summary", 
        Guid? employeeId = null, 
        CancellationToken cancellationToken = default)
    {
        var report = await GetLeaveReportAsync(startDate, endDate, period, employeeId, cancellationToken);
        var sb = new StringBuilder();

        var isDetailed = viewType?.Equals("detailed", StringComparison.OrdinalIgnoreCase) == true;

        if (isDetailed)
        {
            sb.AppendLine($"# PUNNYA HEART FOUNDATION - ITEMIZED LEAVE REQUESTS AUDIT LOG");
            sb.AppendLine($"# Period: {report.Period} ({report.StartDate} to {report.EndDate})");
            sb.AppendLine($"# Total Applications: {report.TotalApplications} | Approved Days: {report.TotalApprovedDays} | Pending: {report.TotalPendingRequests} | Rejected: {report.TotalRejectedRequests}");
            sb.AppendLine();

            sb.AppendLine("Leave ID,Employee Name,Email,Leave Type,Start Date,End Date,Total Days,Reason,Status,Reviewer,Review Comments,Applied At,Reviewed At");

            foreach (var d in report.DetailedRecords)
            {
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\",{6},\"{7}\",\"{8}\",\"{9}\",\"{10}\",\"{11}\",\"{12}\"",
                    d.Id,
                    d.EmployeeName.Replace("\"", "\"\""),
                    d.Email,
                    d.LeaveType,
                    d.StartDate,
                    d.EndDate,
                    d.TotalDays,
                    d.Reason.Replace("\"", "\"\""),
                    d.Status,
                    (d.ReviewerName ?? "—").Replace("\"", "\"\""),
                    (d.ReviewComments ?? "—").Replace("\"", "\"\""),
                    d.AppliedAt,
                    d.ReviewedAt ?? "—"
                ));
            }
        }
        else
        {
            sb.AppendLine($"# PUNNYA HEART FOUNDATION - MONTH-END / PERIOD LEAVE SUMMARY");
            sb.AppendLine($"# Period: {report.Period} ({report.StartDate} to {report.EndDate})");
            sb.AppendLine($"# Total Applications: {report.TotalApplications} | Approved Days: {report.TotalApprovedDays} | Pending: {report.TotalPendingRequests} | Rejected: {report.TotalRejectedRequests}");
            sb.AppendLine();

            sb.AppendLine("Employee Name,Email,Station ID,Total Applications,Approved Days,Casual Leave Days,Sick Leave Days,Medical Leave Days,Annual Leave Days,Emergency Leave Days,Pending Requests,Rejected Requests");

            foreach (var s in report.StaffSummaries)
            {
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "\"{0}\",\"{1}\",\"{2}\",{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                    s.EmployeeName.Replace("\"", "\"\""),
                    s.Email,
                    s.StationId,
                    s.TotalRequests,
                    s.TotalApprovedDays,
                    s.CasualLeaveDays,
                    s.SickLeaveDays,
                    s.MedicalLeaveDays,
                    s.AnnualLeaveDays,
                    s.EmergencyLeaveDays,
                    s.PendingRequests,
                    s.RejectedRequests
                ));
            }
        }

        var utf8WithBom = new UTF8Encoding(true);
        return utf8WithBom.GetBytes(sb.ToString());
    }

    #endregion

    #region Helper Methods

    private static (DateTime rangeStart, DateTime rangeEnd, string periodLabel) ResolveDateRange(DateTime? startDate, DateTime? endDate, string? period)
    {
        var now = DateTime.UtcNow;
        var normalized = (period ?? "MONTH").Trim().ToUpperInvariant();

        DateTime rangeStart;
        DateTime rangeEnd;
        string label;

        if (normalized == "TODAY")
        {
            rangeStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddDays(1);
            label = rangeStart.ToString("yyyy-MM-dd");
        }
        else if (normalized == "YESTERDAY")
        {
            rangeStart = DateTime.SpecifyKind(now.Date.AddDays(-1), DateTimeKind.Utc);
            rangeEnd = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
            label = rangeStart.ToString("yyyy-MM-dd");
        }
        else if (normalized == "WEEK")
        {
            int diff = (7 + (now.Date.DayOfWeek - DayOfWeek.Monday)) % 7;
            rangeStart = DateTime.SpecifyKind(now.Date.AddDays(-diff), DateTimeKind.Utc);
            rangeEnd = rangeStart.AddDays(7);
            label = $"Week of {rangeStart:yyyy-MM-dd}";
        }
        else if (normalized == "MONTH")
        {
            var refDate = startDate ?? now;
            rangeStart = new DateTime(refDate.Year, refDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddMonths(1);
            label = rangeStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        }
        else if (normalized == "LAST_MONTH")
        {
            var firstThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            rangeStart = firstThisMonth.AddMonths(-1);
            rangeEnd = firstThisMonth;
            label = rangeStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        }
        else if (normalized == "YEAR")
        {
            var refDate = startDate ?? now;
            rangeStart = new DateTime(refDate.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddYears(1);
            label = $"Year {rangeStart.Year}";
        }
        else if (startDate.HasValue && endDate.HasValue)
        {
            rangeStart = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            rangeEnd = DateTime.SpecifyKind(endDate.Value.Date.AddDays(1), DateTimeKind.Utc);
            label = $"{rangeStart:yyyy-MM-dd} to {rangeEnd.AddDays(-1):yyyy-MM-dd}";
        }
        else if (startDate.HasValue)
        {
            rangeStart = DateTime.SpecifyKind(startDate.Value.Date, DateTimeKind.Utc);
            rangeEnd = rangeStart.AddDays(1);
            label = rangeStart.ToString("yyyy-MM-dd");
        }
        else
        {
            var firstThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            rangeStart = firstThisMonth;
            rangeEnd = rangeStart.AddMonths(1);
            label = rangeStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        }

        if (rangeStart > rangeEnd)
        {
            var temp = rangeStart;
            rangeStart = rangeEnd.AddDays(-1);
            rangeEnd = temp.AddDays(1);
        }

        return (rangeStart, rangeEnd, label);
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
        var ist = utcTime.Value.AddHours(5.5);
        return ist.ToString("hh:mm:ss tt");
    }

    #endregion
}
