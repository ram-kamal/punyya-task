using System.ComponentModel.DataAnnotations;
using PHF.Core.Enums;

namespace PHF.Core.DTOs;

public record AppActivityLogDto
{
    public Guid? TaskId { get; init; }
    public string? TaskCode { get; init; }

    [Required]
    public string ProcessName { get; init; } = string.Empty;

    public string? WindowTitle { get; init; }

    public int DurationSeconds { get; init; }

    public bool IsIdle { get; init; }

    public DateTime LoggedAt { get; init; }
}

public record UserSessionDto
{
    [Required]
    public string MachineName { get; init; } = string.Empty;

    [Required]
    public string EventType { get; init; } = string.Empty; // LOGON, LOGOFF, LOCK, UNLOCK

    public DateTime EventTimestamp { get; init; }
}

public record TelemetryBatchRequest
{
    public string? StationId { get; init; }
    public Guid? UserId { get; init; }
    public List<UserSessionDto> SessionEvents { get; init; } = new();
    public List<AppActivityLogDto> ActivityLogs { get; init; } = new();
}

public record WorkstationHeartbeatDto
{
    [Required]
    public string StationId { get; init; } = string.Empty;

    public Guid? UserId { get; init; }
    public string? UserFullName { get; init; }
    public string? CurrentTaskCode { get; init; }
    public string? CurrentTaskTitle { get; init; }
    public string? ActiveProcess { get; init; }
    public string? WindowTitle { get; init; }
    public string State { get; init; } = StationState.ACTIVE.ToString();
}

public record WorkstationStatusDto
{
    public string StationId { get; init; } = string.Empty;
    public Guid? UserId { get; init; }
    public string? FullName { get; init; }
    public string? CurrentTaskCode { get; init; }
    public string? CurrentTaskTitle { get; init; }
    public string? FocusedApp { get; init; }
    public string? WindowTitle { get; init; }
    public string State { get; init; } = StationState.OFFLINE.ToString();
    public DateTime LastSeen { get; init; }
    public int ActiveMinutesToday { get; init; }
    public int IdleMinutesToday { get; init; }
}

public record LiveWorkstationEventDto
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string StationId { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string EventType { get; init; } = string.Empty; // FOCUS_CHANGED, SESSION_LOCKED, SESSION_UNLOCKED, TASK_SWITCHED, CLOCK_IN, CLOCK_OUT
    public string Details { get; init; } = string.Empty;
    public string? ProcessName { get; init; }
    public string? TaskCode { get; init; }
}
