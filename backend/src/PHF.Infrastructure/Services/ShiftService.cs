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

        // Check if there is already an active shift today
        var existingActive = await _dbContext.Shifts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.ACTIVE.ToString(), cancellationToken);

        if (existingActive != null)
        {
            return await MapToDto(existingActive, cancellationToken);
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

        // Calculate shift active time from logs if needed
        var shiftLogs = await _dbContext.AppActivityLogs
            .Where(l => l.UserId == userId && l.LoggedAt >= shift.ClockIn && l.LoggedAt <= shift.ClockOut.Value)
            .ToListAsync(cancellationToken);

        shift.TotalActiveSeconds = shiftLogs.Where(l => !l.IsIdle).Sum(l => l.DurationSeconds);
        shift.TotalIdleSeconds = shiftLogs.Where(l => l.IsIdle).Sum(l => l.DurationSeconds);

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

        var totalSecondsToday = todayLogs.Sum(l => l.DurationSeconds);
        if (totalSecondsToday == 0 && shift.Status == ShiftStatus.ACTIVE.ToString())
        {
            totalSecondsToday = (int)(DateTime.UtcNow - shift.ClockIn).TotalSeconds;
        }

        return new ShiftResponseDto
        {
            Id = shift.Id,
            UserId = shift.UserId,
            StationId = shift.StationId,
            ClockIn = shift.ClockIn,
            ClockOut = shift.ClockOut,
            TotalActiveSeconds = shift.TotalActiveSeconds,
            TotalIdleSeconds = shift.TotalIdleSeconds,
            Status = shift.Status,
            TotalHoursToday = Math.Round(totalSecondsToday / 3600.0, 2)
        };
    }
}
