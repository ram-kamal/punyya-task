using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class ShiftService : IShiftService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public ShiftService(ApplicationDbContext dbContext, IRealtimeNotifier realtimeNotifier)
    {
        _dbContext = dbContext;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<ShiftResponseDto> ClockInAsync(Guid userId, string stationId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);

        // Check if there is already an active shift
        var existingActive = await _dbContext.Shifts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.ACTIVE.ToString(), cancellationToken);

        if (existingActive != null)
        {
            // If active shift has exceeded 12 hours or is from a previous calendar day, auto-close it and create a fresh one for today
            if (existingActive.ClockIn.Date < DateTime.UtcNow.Date || (DateTime.UtcNow - existingActive.ClockIn) >= TimeSpan.FromHours(12))
            {
                var autoOut = existingActive.ClockIn.AddHours(8);
                if (autoOut > DateTime.UtcNow) autoOut = DateTime.UtcNow;
                existingActive.ClockOut = autoOut;
                existingActive.Status = ShiftStatus.COMPLETED.ToString();
                var elapsed = Math.Max(0, (int)(existingActive.ClockOut.Value - existingActive.ClockIn).TotalSeconds);
                if (existingActive.TotalActiveSeconds == 0)
                {
                    existingActive.TotalActiveSeconds = Math.Min(elapsed, 28800);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                return await MapToDto(existingActive, cancellationToken);
            }
        }

        var shift = new Shift
        {
            UserId = userId,
            StationId = !string.IsNullOrWhiteSpace(stationId) ? stationId : (user?.StationId ?? "STATION"),
            ClockIn = DateTime.UtcNow,
            Status = ShiftStatus.ACTIVE.ToString()
        };

        _dbContext.Shifts.Add(shift);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = shift.StationId,
            UserName = user?.FullName,
            UserEmail = user?.Email,
            EventType = "CLOCK_IN",
            Details = $"{user?.FullName ?? "Employee"} ({user?.Email ?? shift.StationId}) clocked in"
        });

        return await MapToDto(shift, cancellationToken);
    }

    public async Task<ShiftResponseDto?> ClockOutAsync(Guid userId, string stationId, CancellationToken cancellationToken = default)
    {
        var shift = await _dbContext.Shifts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.ACTIVE.ToString(), cancellationToken);

        if (shift == null) return null;

        shift.ClockOut = DateTime.UtcNow;
        shift.Status = ShiftStatus.COMPLETED.ToString();

        var elapsedShiftSeconds = Math.Max(0, (int)(shift.ClockOut.Value - shift.ClockIn).TotalSeconds);

        // Calculate shift active time from aggregated logs if needed
        var shiftLogStats = await _dbContext.AppActivityLogs
            .Where(l => l.UserId == userId && l.LoggedAt >= shift.ClockIn && l.LoggedAt <= shift.ClockOut.Value)
            .GroupBy(l => l.IsIdle)
            .Select(g => new { IsIdle = g.Key, TotalSeconds = g.Sum(l => l.DurationSeconds) })
            .ToListAsync(cancellationToken);

        var loggedActive = shiftLogStats.FirstOrDefault(s => !s.IsIdle)?.TotalSeconds ?? 0;
        var loggedIdle = shiftLogStats.FirstOrDefault(s => s.IsIdle)?.TotalSeconds ?? 0;

        if (loggedActive + loggedIdle > 0)
        {
            if (loggedActive + loggedIdle > elapsedShiftSeconds)
            {
                double activeRatio = (double)loggedActive / (loggedActive + loggedIdle);
                shift.TotalActiveSeconds = (int)Math.Round(elapsedShiftSeconds * activeRatio);
                shift.TotalIdleSeconds = Math.Max(0, elapsedShiftSeconds - shift.TotalActiveSeconds);
            }
            else
            {
                shift.TotalIdleSeconds = loggedIdle;
                shift.TotalActiveSeconds = Math.Max(loggedActive, Math.Max(0, elapsedShiftSeconds - loggedIdle));
            }
        }
        else
        {
            shift.TotalActiveSeconds = elapsedShiftSeconds;
            shift.TotalIdleSeconds = 0;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var user = await _dbContext.Users.FindAsync(new object[] { userId }, cancellationToken);
        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = shift.StationId,
            UserName = user?.FullName,
            UserEmail = user?.Email,
            EventType = "CLOCK_OUT",
            Details = $"{user?.FullName ?? "Employee"} ({user?.Email ?? shift.StationId}) clocked out. Shift ended."
        });

        return await MapToDto(shift, cancellationToken);
    }

    public async Task<ShiftResponseDto?> GetCurrentShiftAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var todayEnd = today.AddDays(1);

        // First find if there is an active shift
        var activeShift = await _dbContext.Shifts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.ACTIVE.ToString(), cancellationToken);

        if (activeShift != null)
        {
            // Auto-close if older than 12 hours or from previous calendar day
            if (activeShift.ClockIn.Date < today || (DateTime.UtcNow - activeShift.ClockIn) >= TimeSpan.FromHours(12))
            {
                var autoOut = activeShift.ClockIn.AddHours(8);
                if (autoOut > DateTime.UtcNow) autoOut = DateTime.UtcNow;
                activeShift.ClockOut = autoOut;
                activeShift.Status = ShiftStatus.COMPLETED.ToString();
                var elapsed = Math.Max(0, (int)(activeShift.ClockOut.Value - activeShift.ClockIn).TotalSeconds);
                if (activeShift.TotalActiveSeconds == 0)
                {
                    activeShift.TotalActiveSeconds = Math.Min(elapsed, 28800);
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                return await MapToDto(activeShift, cancellationToken);
            }
        }

        // If no active shift, look for the most recent shift overlapping today
        var latestTodayShift = await _dbContext.Shifts
            .Where(s => s.UserId == userId && s.ClockIn < todayEnd && (s.ClockOut == null || s.ClockOut >= today))
            .OrderByDescending(s => s.ClockIn)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestTodayShift != null)
        {
            return await MapToDto(latestTodayShift, cancellationToken);
        }

        return null;
    }

    public async Task<int> AutoCloseStaleShiftsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-12);
        var todayUtc = DateTime.UtcNow.Date;

        var staleShifts = await _dbContext.Shifts
            .Where(s => s.Status == ShiftStatus.ACTIVE.ToString() && (s.ClockIn < cutoff || s.ClockIn.Date < todayUtc))
            .ToListAsync(cancellationToken);

        if (!staleShifts.Any()) return 0;

        foreach (var shift in staleShifts)
        {
            var autoOut = shift.ClockIn.AddHours(8);
            if (autoOut > DateTime.UtcNow) autoOut = DateTime.UtcNow;
            shift.ClockOut = autoOut;
            shift.Status = ShiftStatus.COMPLETED.ToString();

            var elapsedShiftSeconds = Math.Max(0, (int)(shift.ClockOut.Value - shift.ClockIn).TotalSeconds);

            var shiftLogStats = await _dbContext.AppActivityLogs
                .Where(l => l.UserId == shift.UserId && l.LoggedAt >= shift.ClockIn && l.LoggedAt <= shift.ClockOut.Value)
                .GroupBy(l => l.IsIdle)
                .Select(g => new { IsIdle = g.Key, TotalSeconds = g.Sum(l => l.DurationSeconds) })
                .ToListAsync(cancellationToken);

            var loggedActive = shiftLogStats.FirstOrDefault(s => !s.IsIdle)?.TotalSeconds ?? 0;
            var loggedIdle = shiftLogStats.FirstOrDefault(s => s.IsIdle)?.TotalSeconds ?? 0;

            if (loggedActive + loggedIdle > 0)
            {
                shift.TotalIdleSeconds = Math.Min(elapsedShiftSeconds, loggedIdle);
                shift.TotalActiveSeconds = Math.Min(elapsedShiftSeconds - shift.TotalIdleSeconds, Math.Max(loggedActive, 0));
            }
            else
            {
                shift.TotalActiveSeconds = Math.Min(elapsedShiftSeconds, 28800);
                shift.TotalIdleSeconds = 0;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return staleShifts.Count;
    }

    private async Task<ShiftResponseDto> MapToDto(Shift shift, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var todayEnd = today.AddDays(1);

        // Fetch all shifts for this user today (overlapping [today, todayEnd))
        var todayShifts = await _dbContext.Shifts
            .Where(s => s.UserId == shift.UserId && s.ClockIn < todayEnd && (s.ClockOut == null || s.ClockOut >= today))
            .OrderBy(s => s.ClockIn)
            .ToListAsync(cancellationToken);

        var todayLogStats = await _dbContext.AppActivityLogs
            .Where(l => l.UserId == shift.UserId && l.LoggedAt >= today && l.LoggedAt < todayEnd)
            .GroupBy(l => l.IsIdle)
            .Select(g => new { IsIdle = g.Key, TotalSeconds = g.Sum(l => l.DurationSeconds) })
            .ToListAsync(cancellationToken);

        var loggedActive = todayLogStats.FirstOrDefault(s => !s.IsIdle)?.TotalSeconds ?? 0;
        var loggedIdle = todayLogStats.FirstOrDefault(s => s.IsIdle)?.TotalSeconds ?? 0;

        // Separate prior completed shifts from this shift
        int priorSecondsToday = 0;
        int priorActiveSecondsToday = 0;
        int priorIdleSecondsToday = 0;

        foreach (var s in todayShifts.Where(s => s.Id != shift.Id))
        {
            var sEnd = s.ClockOut ?? (s.Status == ShiftStatus.ACTIVE.ToString() ? DateTime.UtcNow : s.ClockIn);
            var sElapsed = Math.Max(0, (int)(sEnd - s.ClockIn).TotalSeconds);
            var sIdle = Math.Min(sElapsed, Math.Max(0, s.TotalIdleSeconds));
            var sAct = s.TotalActiveSeconds > 0 ? Math.Min(sElapsed - sIdle, s.TotalActiveSeconds) : Math.Max(0, sElapsed - sIdle);

            priorSecondsToday += sElapsed;
            priorActiveSecondsToday += sAct;
            priorIdleSecondsToday += sIdle;
        }

        // For this specific shift
        var shiftEnd = shift.ClockOut ?? (shift.Status == ShiftStatus.ACTIVE.ToString() ? DateTime.UtcNow : shift.ClockIn);
        var thisShiftElapsed = Math.Max(0, (int)(shiftEnd - shift.ClockIn).TotalSeconds);
        var thisShiftIdle = Math.Min(thisShiftElapsed, Math.Max(0, shift.TotalIdleSeconds));
        var thisShiftActive = shift.TotalActiveSeconds > 0 
            ? Math.Min(thisShiftElapsed - thisShiftIdle, shift.TotalActiveSeconds) 
            : Math.Max(0, thisShiftElapsed - thisShiftIdle);

        // Combined Total: Old Time + New Time!
        int totalCombinedSecondsToday = priorSecondsToday + thisShiftElapsed;
        int totalCombinedActiveToday = priorActiveSecondsToday + thisShiftActive;
        int totalCombinedIdleToday = priorIdleSecondsToday + thisShiftIdle;

        // If activity logs recorded active/idle time, reconcile with telemetry
        if (loggedActive + loggedIdle > 0)
        {
            if (loggedActive + loggedIdle > totalCombinedSecondsToday)
            {
                double activeRatio = (double)loggedActive / (loggedActive + loggedIdle);
                totalCombinedActiveToday = (int)Math.Round(totalCombinedSecondsToday * activeRatio);
                totalCombinedIdleToday = Math.Max(0, totalCombinedSecondsToday - totalCombinedActiveToday);
            }
            else
            {
                totalCombinedIdleToday = Math.Min(totalCombinedSecondsToday, Math.Max(totalCombinedIdleToday, loggedIdle));
                totalCombinedActiveToday = Math.Max(totalCombinedActiveToday, totalCombinedSecondsToday - totalCombinedIdleToday);
            }
        }

        return new ShiftResponseDto
        {
            Id = shift.Id,
            UserId = shift.UserId,
            StationId = shift.StationId,
            ClockIn = shift.ClockIn,
            ClockOut = shift.ClockOut,
            TotalActiveSeconds = thisShiftActive,
            TotalIdleSeconds = thisShiftIdle,
            Status = shift.Status,
            TotalHoursToday = Math.Round(totalCombinedSecondsToday / 3600.0, 2),
            ActiveTimeFormatted = FormatHoursMinutes(totalCombinedActiveToday),
            TotalTimeFormatted = FormatHoursMinutes(totalCombinedSecondsToday),
            PriorSecondsToday = priorSecondsToday,
            TotalSecondsToday = totalCombinedSecondsToday,
            ActiveSecondsToday = totalCombinedActiveToday,
            IdleSecondsToday = totalCombinedIdleToday
        };
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
