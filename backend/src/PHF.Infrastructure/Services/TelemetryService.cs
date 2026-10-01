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

    // Concurrent in-memory state tracking for live employees / users (User-based workforce oversight)
    private static readonly ConcurrentDictionary<Guid, UserLiveStateInternal> _liveUsers = new();
    private static readonly ConcurrentDictionary<string, Guid> _stationUserIndex = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentQueue<LiveWorkstationEventDto> _recentEvents = new();

    private class UserLiveStateInternal
    {
        public Guid UserId { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? CurrentTaskCode { get; set; }
        public string? CurrentTaskTitle { get; set; }
        public string? FocusedApp { get; set; }
        public string? WindowTitle { get; set; }
        public string State { get; set; } = StationState.ACTIVE.ToString();
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public int ActiveMinutesToday { get; set; }
        public int IdleMinutesToday { get; set; }
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

        // Ensure userId exists in DB to prevent foreign key constraint violation
        if (userId.HasValue && !await _dbContext.Users.AnyAsync(u => u.Id == userId.Value, cancellationToken))
        {
            userId = null;
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
                    EventTimestamp = EnsureUtc(sessionDto.EventTimestamp),
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
            var validTaskIds = new HashSet<Guid>();
            var taskCodeCache = new Dictionary<string, Guid>();

            foreach (var logDto in request.ActivityLogs)
            {
                Guid? resolvedTaskId = null;

                if (logDto.TaskId.HasValue)
                {
                    if (validTaskIds.Contains(logDto.TaskId.Value))
                    {
                        resolvedTaskId = logDto.TaskId.Value;
                    }
                    else if (await _dbContext.Tasks.AnyAsync(t => t.Id == logDto.TaskId.Value, cancellationToken))
                    {
                        validTaskIds.Add(logDto.TaskId.Value);
                        resolvedTaskId = logDto.TaskId.Value;
                    }
                }

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
                            validTaskIds.Add(task.Id);
                        }
                    }
                }

                var (sanitizedProc, sanitizedTitle) = SanitizeProcessAndTitle(logDto.ProcessName, logDto.WindowTitle);

                var activityLog = new AppActivityLog
                {
                    UserId = userId,
                    TaskId = resolvedTaskId,
                    ProcessName = sanitizedProc,
                    WindowTitle = sanitizedTitle,
                    DurationSeconds = logDto.DurationSeconds,
                    IsIdle = logDto.IsIdle,
                    LoggedAt = EnsureUtc(logDto.LoggedAt),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.AppActivityLogs.Add(activityLog);
                count++;

                // Broadcast live focus change only for recent real-time activity (not old historical buffer flushes)
                if (!logDto.IsIdle && !string.IsNullOrWhiteSpace(sanitizedProc) && activityLog.LoggedAt >= DateTime.UtcNow.AddMinutes(-2))
                {
                    var taskSuffix = !string.IsNullOrWhiteSpace(logDto.TaskCode) ? $" ({logDto.TaskCode})" : "";
                    var liveEvent = new LiveWorkstationEventDto
                    {
                        StationId = request.StationId ?? "STATION",
                        EventType = "FOCUS_CHANGED",
                        Details = $"focus changed → {sanitizedProc}{taskSuffix}",
                        ProcessName = sanitizedProc,
                        TaskCode = logDto.TaskCode
                    };

                    RecordLiveEvent(liveEvent);
                    await _realtimeNotifier.BroadcastWorkstationEventAsync(liveEvent);
                }
            }
        }

        if (count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        return count;
    }

    private static DateTime EnsureUtc(DateTime dt)
    {
        if (dt == default) return DateTime.UtcNow;
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
    }

    public async Task ProcessHeartbeatAsync(WorkstationHeartbeatDto heartbeat, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        Guid? resolvedUserId = heartbeat.UserId;

        if (!string.IsNullOrWhiteSpace(heartbeat.StationId) && resolvedUserId.HasValue)
        {
            _stationUserIndex[heartbeat.StationId] = resolvedUserId.Value;
        }

        // Fallback resolution if UserId is missing
        if (!resolvedUserId.HasValue && !string.IsNullOrWhiteSpace(heartbeat.StationId))
        {
            if (_stationUserIndex.TryGetValue(heartbeat.StationId, out var mappedId))
            {
                resolvedUserId = mappedId;
            }
            else
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.StationId == heartbeat.StationId, cancellationToken);
                resolvedUserId = user?.Id;
                if (resolvedUserId.HasValue)
                {
                    _stationUserIndex[heartbeat.StationId] = resolvedUserId.Value;
                }
            }
        }

        if (resolvedUserId.HasValue)
        {
            var userId = resolvedUserId.Value;
            var isExisting = _liveUsers.TryGetValue(userId, out var existingState);
            var state = _liveUsers.GetOrAdd(userId, uid => new UserLiveStateInternal
            {
                UserId = uid,
                StationId = heartbeat.StationId,
                LastSeen = now
            });

            // Calculate seconds elapsed since last heartbeat (typically ~15s, clamped to max 60s)
            if (isExisting && existingState != null)
            {
                var elapsedSec = Math.Clamp((int)(now - existingState.LastSeen).TotalSeconds, 0, 60);
                if (elapsedSec > 0)
                {
                    if (heartbeat.State == StationState.IDLE.ToString())
                    {
                        state.IdleMinutesToday += (int)Math.Round((double)elapsedSec / 60);
                    }
                    else if (heartbeat.State == StationState.ACTIVE.ToString())
                    {
                        state.ActiveMinutesToday += (int)Math.Round((double)elapsedSec / 60);
                    }

                    // Accumulate into active shift in database so timesheet shows live accurate idle/active seconds!
                    var activeShift = await _dbContext.Shifts
                        .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.ACTIVE.ToString(), cancellationToken);
                    if (activeShift != null)
                    {
                        if (heartbeat.State == StationState.IDLE.ToString())
                        {
                            activeShift.TotalIdleSeconds += elapsedSec;
                        }
                        else if (heartbeat.State == StationState.ACTIVE.ToString())
                        {
                            activeShift.TotalActiveSeconds += elapsedSec;
                        }
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }

            var (sanitizedProc, sanitizedTitle) = SanitizeProcessAndTitle(heartbeat.ActiveProcess, heartbeat.WindowTitle);

            state.StationId = !string.IsNullOrWhiteSpace(heartbeat.StationId) ? heartbeat.StationId : state.StationId;
            state.LastSeen = now;
            state.State = heartbeat.State;
            if (!string.IsNullOrWhiteSpace(sanitizedProc) && sanitizedProc != "—")
            {
                state.FocusedApp = sanitizedProc;
            }
            else if (heartbeat.State == StationState.ACTIVE.ToString())
            {
                state.FocusedApp = "Windows Desktop";
            }

            if (!string.IsNullOrWhiteSpace(sanitizedTitle) && sanitizedTitle != "—")
            {
                state.WindowTitle = sanitizedTitle;
            }
            else if (heartbeat.State == StationState.ACTIVE.ToString())
            {
                state.WindowTitle = "Windows Desktop";
            }
            state.CurrentTaskCode = heartbeat.CurrentTaskCode ?? state.CurrentTaskCode;
            state.CurrentTaskTitle = heartbeat.CurrentTaskTitle ?? state.CurrentTaskTitle;

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
                LastSeen = state.LastSeen,
                ActiveMinutesToday = state.ActiveMinutesToday,
                IdleMinutesToday = state.IdleMinutesToday
            };

            await _realtimeNotifier.BroadcastWorkstationStatusAsync(statusDto);
        }
    }

    public async Task<List<WorkstationStatusDto>> GetWorkstationsStatusAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        // Fetch all active employees / staff members (User-based tracking!)
        var employees = await _dbContext.Users
            .Where(u => u.IsActive && 
                       (u.Role == UserRole.EMPLOYEE.ToString() || 
                        _dbContext.Shifts.Any(s => s.UserId == u.Id)))
            .OrderBy(u => u.FullName)
            .ToListAsync(cancellationToken);

        var todayLogs = await _dbContext.AppActivityLogs
            .Where(l => l.LoggedAt >= today)
            .ToListAsync(cancellationToken);

        var todayShifts = await _dbContext.Shifts
            .Where(s => s.ClockIn >= today)
            .ToListAsync(cancellationToken);

        var result = new List<WorkstationStatusDto>();

        foreach (var emp in employees)
        {
            var empShifts = todayShifts.Where(s => s.UserId == emp.Id).ToList();
            var activeShift = empShifts.FirstOrDefault(s => s.Status == ShiftStatus.ACTIVE.ToString());

            var hasLive = _liveUsers.TryGetValue(emp.Id, out var live);

            // USER-BASED STATE LOGIC:
            // 1. If employee has no active shift today (clocked out or hasn't clocked in):
            //    -> State is ALWAYS OFFLINE! (Eliminates ghost active states for other employees sharing a machine).
            // 2. If employee has an active shift:
            //    - If heartbeat received within 3 minutes: State is ACTIVE (or IDLE if idle threshold reached).
            //    - If no heartbeat for > 3 minutes: State is OFFLINE (Away / PC suspended).
            var state = StationState.OFFLINE.ToString();
            var focusedApp = "—";
            var windowTitle = "";
            string? taskCode = null;
            string? taskTitle = null;
            var lastSeen = live?.LastSeen ?? (activeShift?.ClockIn ?? today);
            var currentStation = live?.StationId ?? emp.StationId ?? "STATION";

            if (activeShift != null)
            {
                if (hasLive && (now - live!.LastSeen).TotalMinutes <= 3)
                {
                    var (sanitizedProc, sanitizedTitle) = SanitizeProcessAndTitle(live.FocusedApp, live.WindowTitle);
                    state = live.State;
                    focusedApp = string.IsNullOrWhiteSpace(sanitizedProc) || sanitizedProc == "—" ? "Windows Desktop" : sanitizedProc;
                    windowTitle = string.IsNullOrWhiteSpace(sanitizedTitle) || sanitizedTitle == "—" ? "Windows Desktop" : sanitizedTitle;
                    taskCode = live.CurrentTaskCode;
                    taskTitle = live.CurrentTaskTitle;
                    if (!string.IsNullOrWhiteSpace(live.StationId))
                    {
                        currentStation = live.StationId;
                    }
                }
                else
                {
                    state = StationState.OFFLINE.ToString();
                }
            }

            // Fallback task code if not populated in live state
            if (string.IsNullOrWhiteSpace(taskCode))
            {
                var activeTask = await _dbContext.Tasks
                    .FirstOrDefaultAsync(t => t.AssignedTo == emp.Id && t.Status == TaskItemStatus.IN_PROGRESS.ToString(), cancellationToken);
                if (activeTask != null)
                {
                    taskCode = activeTask.TaskCode;
                    taskTitle = activeTask.Title;
                }
            }

            // Calculate active and idle minutes today for this user (matching TimesheetService exactly)
            var empLogs = todayLogs.Where(l => l.UserId == emp.Id).ToList();
            var loggedActiveMins = empLogs.Where(l => !l.IsIdle).Sum(l => l.DurationSeconds) / 60;
            var loggedIdleMins = empLogs.Where(l => l.IsIdle).Sum(l => l.DurationSeconds) / 60;

            var shiftTotalMins = 0;
            var shiftActiveMins = 0;
            var shiftIdleMins = 0;

            foreach (var s in empShifts)
            {
                var sEnd = s.ClockOut ?? (s.Status == ShiftStatus.ACTIVE.ToString() ? now : s.ClockIn);
                var sElapsedMins = Math.Max(0, (int)(sEnd - s.ClockIn).TotalMinutes);
                var sIdleM = s.TotalIdleSeconds / 60;
                var sActM = s.TotalActiveSeconds > 0 ? s.TotalActiveSeconds / 60 : Math.Max(0, sElapsedMins - sIdleM);
                if (sActM + sIdleM < sElapsedMins)
                {
                    sActM = Math.Max(sActM, sElapsedMins - sIdleM);
                }

                shiftTotalMins += sElapsedMins;
                shiftActiveMins += sActM;
                shiftIdleMins += sIdleM;
            }

            int activeMinutes;
            int idleMinutes;

            if (empShifts.Any())
            {
                var rawActiveM = Math.Max(loggedActiveMins, shiftActiveMins);
                var rawIdleM = Math.Max(loggedIdleMins, shiftIdleMins);

                if (rawActiveM + rawIdleM > 0)
                {
                    if (rawActiveM + rawIdleM > shiftTotalMins)
                    {
                        double activeRatio = (double)rawActiveM / (rawActiveM + rawIdleM);
                        activeMinutes = (int)Math.Round(shiftTotalMins * activeRatio);
                        idleMinutes = Math.Max(0, shiftTotalMins - activeMinutes);
                    }
                    else
                    {
                        idleMinutes = Math.Min(shiftTotalMins, rawIdleM);
                        activeMinutes = Math.Max(rawActiveM, shiftTotalMins - idleMinutes);
                    }
                }
                else
                {
                    activeMinutes = shiftTotalMins;
                    idleMinutes = 0;
                }
            }
            else
            {
                activeMinutes = 0;
                idleMinutes = 0;
            }

            if (hasLive && live != null)
            {
                live.ActiveMinutesToday = activeMinutes;
                live.IdleMinutesToday = idleMinutes;
            }
            else
            {
                var entry = _liveUsers.GetOrAdd(emp.Id, uid => new UserLiveStateInternal
                {
                    UserId = uid,
                    StationId = currentStation,
                    FullName = emp.FullName
                });
                entry.ActiveMinutesToday = activeMinutes;
                entry.IdleMinutesToday = idleMinutes;
            }

            result.Add(new WorkstationStatusDto
            {
                StationId = currentStation,
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

    private static (string ProcessName, string WindowTitle) SanitizeProcessAndTitle(string? processName, string? windowTitle)
    {
        var proc = processName?.Trim() ?? "";
        var title = windowTitle?.Trim() ?? "";

        if (proc.Contains("UnlockingWindow", StringComparison.OrdinalIgnoreCase) ||
            proc.Contains("Program Manager", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("lockapp", StringComparison.OrdinalIgnoreCase) ||
            proc.Equals("logonui", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("UnlockingWindow", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Program Manager", StringComparison.OrdinalIgnoreCase))
        {
            return ("Windows Desktop", "Windows Desktop");
        }

        return (proc, title);
    }
}
