using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class TelemetryService : ITelemetryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRealtimeNotifier _realtimeNotifier;

    // Concurrent in-memory state tracking for live workstations
    private static readonly ConcurrentDictionary<string, WorkstationStateInternal> _liveStations = new();
    private static readonly ConcurrentQueue<LiveWorkstationEventDto> _recentEvents = new();

    private class WorkstationStateInternal
    {
        public string StationId { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string? FullName { get; set; }
        public string? CurrentTaskCode { get; set; }
        public string? CurrentTaskTitle { get; set; }
        public string? FocusedApp { get; set; }
        public string? WindowTitle { get; set; }
        public string State { get; set; } = StationState.ACTIVE.ToString();
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }

    public TelemetryService(ApplicationDbContext dbContext, IRealtimeNotifier realtimeNotifier)
    {
        _dbContext = dbContext;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<int> IngestBatchAsync(TelemetryBatchRequest request, CancellationToken cancellationToken = default)
    {
        var count = 0;

        // Resolve userId if not provided
        var userId = request.UserId;
        if (!userId.HasValue && !string.IsNullOrWhiteSpace(request.StationId))
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.StationId == request.StationId, cancellationToken);
            userId = user?.Id;
        }

        // 1. Process User Session Events
        if (request.SessionEvents != null && request.SessionEvents.Any())
        {
            foreach (var sessionDto in request.SessionEvents)
            {
                var session = new UserSession
                {
                    UserId = userId,
                    MachineName = sessionDto.MachineName,
                    EventType = sessionDto.EventType.ToUpperInvariant(),
                    EventTimestamp = sessionDto.EventTimestamp != default ? sessionDto.EventTimestamp : DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserSessions.Add(session);
                count++;

                var liveEvent = new LiveWorkstationEventDto
                {
                    StationId = request.StationId ?? sessionDto.MachineName,
                    EventType = session.EventType,
                    Details = session.EventType switch
                    {
                        "LOCK" => "session locked (idle threshold reached)",
                        "UNLOCK" => "session unlocked by user",
                        "LOGON" => "user session logon",
                        "LOGOFF" => "user session logoff",
                        _ => $"session event {session.EventType}"
                    }
                };

                RecordLiveEvent(liveEvent);
                await _realtimeNotifier.BroadcastWorkstationEventAsync(liveEvent);
            }
        }

        // 2. Process App Activity Logs
        if (request.ActivityLogs != null && request.ActivityLogs.Any())
        {
            // Cache task lookups for task codes
            var taskCodeCache = new Dictionary<string, Guid>();

            foreach (var logDto in request.ActivityLogs)
            {
                Guid? resolvedTaskId = logDto.TaskId;

                if (!resolvedTaskId.HasValue && !string.IsNullOrWhiteSpace(logDto.TaskCode))
                {
                    if (taskCodeCache.TryGetValue(logDto.TaskCode, out var cachedId))
                    {
                        resolvedTaskId = cachedId;
                    }
                    else
                    {
                        var task = await _dbContext.Tasks.FirstOrDefaultAsync(t => t.TaskCode == logDto.TaskCode, cancellationToken);
                        if (task != null)
                        {
                            resolvedTaskId = task.Id;
                            taskCodeCache[logDto.TaskCode] = task.Id;
                        }
                    }
                }

                var activityLog = new AppActivityLog
                {
                    UserId = userId,
                    TaskId = resolvedTaskId,
                    ProcessName = logDto.ProcessName,
                    WindowTitle = logDto.WindowTitle,
                    DurationSeconds = logDto.DurationSeconds,
                    IsIdle = logDto.IsIdle,
                    LoggedAt = logDto.LoggedAt != default ? logDto.LoggedAt : DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.AppActivityLogs.Add(activityLog);
                count++;

                // Broadcast live focus change if not idle and has valid process
                if (!logDto.IsIdle && !string.IsNullOrWhiteSpace(logDto.ProcessName))
                {
                    var taskSuffix = !string.IsNullOrWhiteSpace(logDto.TaskCode) ? $" ({logDto.TaskCode})" : "";
                    var liveEvent = new LiveWorkstationEventDto
                    {
                        StationId = request.StationId ?? "STATION",
                        EventType = "FOCUS_CHANGED",
                        Details = $"focus changed → {logDto.ProcessName}{taskSuffix}",
                        ProcessName = logDto.ProcessName,
                        TaskCode = logDto.TaskCode
                    };

                    RecordLiveEvent(liveEvent);
                    await _realtimeNotifier.BroadcastWorkstationEventAsync(liveEvent);
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return count;
    }

    public async Task ProcessHeartbeatAsync(WorkstationHeartbeatDto heartbeat, CancellationToken cancellationToken = default)
    {
        var stationId = heartbeat.StationId;
        var now = DateTime.UtcNow;

        var state = _liveStations.GetOrAdd(stationId, id => new WorkstationStateInternal
        {
            StationId = id
        });

        state.LastSeen = now;
        state.State = heartbeat.State;
        state.FocusedApp = heartbeat.ActiveProcess ?? state.FocusedApp;
        state.WindowTitle = heartbeat.WindowTitle ?? state.WindowTitle;
        state.CurrentTaskCode = heartbeat.CurrentTaskCode ?? state.CurrentTaskCode;
        state.CurrentTaskTitle = heartbeat.CurrentTaskTitle ?? state.CurrentTaskTitle;

        if (heartbeat.UserId.HasValue)
        {
            state.UserId = heartbeat.UserId;
        }

        if (!string.IsNullOrWhiteSpace(heartbeat.UserFullName))
        {
            state.FullName = heartbeat.UserFullName;
        }

        var statusDto = new WorkstationStatusDto
        {
            StationId = state.StationId,
            UserId = state.UserId,
            FullName = state.FullName,
            CurrentTaskCode = state.CurrentTaskCode,
            CurrentTaskTitle = state.CurrentTaskTitle,
            FocusedApp = state.FocusedApp,
            WindowTitle = state.WindowTitle,
            State = state.State,
            LastSeen = state.LastSeen
        };

        await _realtimeNotifier.BroadcastWorkstationStatusAsync(statusDto);
    }

    public async Task<List<WorkstationStatusDto>> GetWorkstationsStatusAsync(CancellationToken cancellationToken = default)
    {
        var employees = await _dbContext.Users
            .Where(u => u.Role == UserRole.EMPLOYEE.ToString() && !string.IsNullOrWhiteSpace(u.StationId))
            .OrderBy(u => u.StationId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var today = now.Date;

        var todayLogs = await _dbContext.AppActivityLogs
            .Where(l => l.LoggedAt >= today)
            .ToListAsync(cancellationToken);

        var todayShifts = await _dbContext.Shifts
            .Where(s => s.ClockIn >= today)
            .ToListAsync(cancellationToken);

        var result = new List<WorkstationStatusDto>();

        foreach (var emp in employees)
        {
            var stationId = emp.StationId!;
            var hasLive = _liveStations.TryGetValue(stationId, out var live);

            // Determine if station is OFFLINE (> 3 minutes without heartbeat)
            var state = StationState.OFFLINE.ToString();
            var focusedApp = "—";
            var windowTitle = "";
            string? taskCode = null;
            string? taskTitle = null;
            var lastSeen = live?.LastSeen ?? today;

            if (hasLive && (now - live!.LastSeen).TotalMinutes <= 3)
            {
                state = live.State;
                focusedApp = live.FocusedApp ?? "—";
                windowTitle = live.WindowTitle ?? "";
                taskCode = live.CurrentTaskCode;
                taskTitle = live.CurrentTaskTitle;
            }
            else
            {
                // Fallback to recent task if in progress
                var activeTask = await _dbContext.Tasks
                    .FirstOrDefaultAsync(t => t.AssignedTo == emp.Id && t.Status == TaskItemStatus.IN_PROGRESS.ToString(), cancellationToken);
                if (activeTask != null)
                {
                    taskCode = activeTask.TaskCode;
                    taskTitle = activeTask.Title;
                }
            }

            var empLogs = todayLogs.Where(l => l.UserId == emp.Id).ToList();
            var empShifts = todayShifts.Where(s => s.UserId == emp.Id).ToList();
            var activeShift = empShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString());

            var activeMinutes = empLogs.Where(l => !l.IsIdle).Sum(l => l.DurationSeconds) / 60;
            var idleMinutes = empLogs.Where(l => l.IsIdle).Sum(l => l.DurationSeconds) / 60;

            if (activeMinutes == 0 && empShifts.Any())
            {
                activeMinutes = empShifts.Sum(s => s.TotalActiveSeconds) / 60;
                idleMinutes = empShifts.Sum(s => s.TotalIdleSeconds) / 60;
            }

            if (activeShift != null)
            {
                var elapsedMins = Math.Max(0, (int)(now - activeShift.ClockIn).TotalMinutes);
                if (elapsedMins > activeMinutes + idleMinutes)
                {
                    activeMinutes = Math.Max(activeMinutes, elapsedMins - idleMinutes);
                }
            }

            activeMinutes = Math.Max(0, activeMinutes);
            idleMinutes = Math.Max(0, idleMinutes);

            result.Add(new WorkstationStatusDto
            {
                StationId = stationId,
                UserId = emp.Id,
                FullName = emp.FullName,
                CurrentTaskCode = taskCode,
                CurrentTaskTitle = taskTitle,
                FocusedApp = focusedApp,
                WindowTitle = windowTitle,
                State = state,
                LastSeen = lastSeen,
                ActiveMinutesToday = activeMinutes,
                IdleMinutesToday = idleMinutes
            });
        }

        return result;
    }

    public Task<List<LiveWorkstationEventDto>> GetRecentEventsAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var list = _recentEvents.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
        return Task.FromResult(list);
    }

    private static void RecordLiveEvent(LiveWorkstationEventDto evt)
    {
        _recentEvents.Enqueue(evt);
        while (_recentEvents.Count > 200)
        {
            _recentEvents.TryDequeue(out _);
        }
    }
}
