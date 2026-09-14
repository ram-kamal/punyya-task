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
            // If active shift is from a previous calendar day, auto-close it and create a fresh one for today
            if (existingActive.ClockIn.Date < DateTime.UtcNow.Date)
            {
                var autoOut = existingActive.ClockIn.AddHours(8);
                if (autoOut > DateTime.UtcNow) autoOut = DateTime.UtcNow;
                existingActive.ClockOut = autoOut;
                existingActive.Status = ShiftStatus.COMPLETED.ToString();
                var elapsed = Math.Max(0, (int)(existingActive.ClockOut.Value - existingActive.ClockIn).TotalSeconds);
                if (existingActive.TotalActiveSeconds == 0)
                {
                    existingActive.TotalActiveSeconds = elapsed;
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
            EventType = "CLOCK_IN",
            Details = $"Employee clocked in at {shift.StationId}"
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

        // Calculate shift active time from logs if needed
        var shiftLogs = await _dbContext.AppActivityLogs
            .Where(l => l.UserId == userId && l.LoggedAt >= shift.ClockIn && l.LoggedAt <= shift.ClockOut.Value)
            .ToListAsync(cancellationToken);

        var loggedActive = shiftLogs.Where(l => !l.IsIdle).Sum(l => l.DurationSeconds);
        var loggedIdle = shiftLogs.Where(l => l.IsIdle).Sum(l => l.DurationSeconds);

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
            EventType = "CLOCK_OUT",
            Details = $"Employee clocked out. Shift ended."
        });

        return await MapToDto(shift, cancellationToken);
    }

    public async Task<ShiftResponseDto?> GetCurrentShiftAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var shift = await _dbContext.Shifts
            .OrderByDescending(s => s.ClockIn)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        return shift != null ? await MapToDto(shift, cancellationToken) : null;
    }

    private async Task<ShiftResponseDto> MapToDto(Shift shift, CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var todayLogs = await _dbContext.AppActivityLogs
            .Where(l => l.UserId == shift.UserId && l.LoggedAt >= today)
            .ToListAsync(cancellationToken);

        var todayShifts = await _dbContext.Shifts
            .Where(s => s.UserId == shift.UserId && s.ClockIn >= today)
            .ToListAsync(cancellationToken);

        var loggedActive = todayLogs.Where(l => !l.IsIdle).Sum(l => l.DurationSeconds);
        var loggedIdle = todayLogs.Where(l => l.IsIdle).Sum(l => l.DurationSeconds);

        var shiftTotalSec = 0;
        var shiftActiveSec = 0;
        var shiftIdleSec = 0;

        foreach (var s in todayShifts)
        {
            var sEnd = s.ClockOut ?? (s.Status == ShiftStatus.ACTIVE.ToString() ? DateTime.UtcNow : s.ClockIn);
            var sElapsed = Math.Max(0, (int)(sEnd - s.ClockIn).TotalSeconds);
            var sIdle = s.TotalIdleSeconds;
            var sAct = s.TotalActiveSeconds > 0 ? s.TotalActiveSeconds : Math.Max(0, sElapsed - sIdle);
            if (sAct + sIdle < sElapsed)
            {
                sAct = Math.Max(sAct, sElapsed - sIdle);
            }

            shiftTotalSec += sElapsed;
            shiftActiveSec += sAct;
            shiftIdleSec += sIdle;
        }

        int totalSecondsToday;
        int activeSecondsToday;
        int idleSecondsToday;

        if (todayShifts.Any())
        {
            var rawActive = Math.Max(loggedActive, shiftActiveSec);
            var rawIdle = Math.Max(loggedIdle, shiftIdleSec);

            if (rawActive + rawIdle > 0)
            {
                if (rawActive + rawIdle > shiftTotalSec)
                {
                    double activeRatio = (double)rawActive / (rawActive + rawIdle);
                    activeSecondsToday = (int)Math.Round(shiftTotalSec * activeRatio);
                    idleSecondsToday = Math.Max(0, shiftTotalSec - activeSecondsToday);
                }
                else
                {
                    idleSecondsToday = Math.Min(shiftTotalSec, rawIdle);
                    activeSecondsToday = Math.Max(rawActive, shiftTotalSec - idleSecondsToday);
                }
            }
            else
            {
                activeSecondsToday = shiftTotalSec;
                idleSecondsToday = 0;
            }
            totalSecondsToday = shiftTotalSec;
        }
        else
        {
            activeSecondsToday = 0;
            idleSecondsToday = 0;
            totalSecondsToday = 0;
        }

        // For the specific shift requested, compute its active / idle time bounded by its elapsed time
        var shiftEnd = shift.ClockOut ?? (shift.Status == ShiftStatus.ACTIVE.ToString() ? DateTime.UtcNow : shift.ClockIn);
        var shiftElapsedSec = Math.Max(0, (int)(shiftEnd - shift.ClockIn).TotalSeconds);
        var thisShiftIdle = Math.Min(shiftElapsedSec, Math.Max(0, shift.TotalIdleSeconds));
        var thisShiftActive = shift.TotalActiveSeconds > 0 
            ? Math.Min(shiftElapsedSec - thisShiftIdle, shift.TotalActiveSeconds) 
            : Math.Max(0, shiftElapsedSec - thisShiftIdle);

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
            TotalHoursToday = Math.Round(totalSecondsToday / 3600.0, 2),
            ActiveTimeFormatted = FormatHoursMinutes(activeSecondsToday),
            TotalTimeFormatted = FormatHoursMinutes(totalSecondsToday)
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
