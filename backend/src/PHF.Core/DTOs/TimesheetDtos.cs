namespace PHF.Core.DTOs;

public record ShiftClockInRequest
{
    public string StationId { get; init; } = string.Empty;
}

public record ShiftClockOutRequest
{
    public string StationId { get; init; } = string.Empty;
}

public record ShiftResponseDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string StationId { get; init; } = string.Empty;
    public DateTime ClockIn { get; init; }
    public DateTime? ClockOut { get; init; }
    public int TotalActiveSeconds { get; init; }
    public int TotalIdleSeconds { get; init; }
    public string Status { get; init; } = string.Empty;
    public double TotalHoursToday { get; init; }
    public string ActiveTimeFormatted { get; init; } = string.Empty;
    public string TotalTimeFormatted { get; init; } = string.Empty;
}

public record TimesheetRowDto
{
    public string Date { get; init; } = string.Empty;
    public string StationId { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string ClockIn { get; init; } = string.Empty;
    public string ClockOut { get; init; } = string.Empty;
    public double TotalHours { get; init; }
    public double ActiveHours { get; init; }
    public double IdleHours { get; init; }
    public int TotalSeconds { get; init; }
    public int ActiveSeconds { get; init; }
    public int IdleSeconds { get; init; }
    public string TotalTimeFormatted { get; init; } = string.Empty;
    public string ActiveTimeFormatted { get; init; } = string.Empty;
    public string IdleTimeFormatted { get; init; } = string.Empty;
    public double EfficiencyPercentage { get; init; }
    public int TasksCompleted { get; init; }
    public int DaysWorked { get; init; } = 1;
}

public record TimesheetSummaryDto
{
    public string Period { get; init; } = "DAY";
    public string StartDate { get; init; } = string.Empty;
    public string EndDate { get; init; } = string.Empty;
    public int ActiveStationsCount { get; init; }
    public int TotalStationsCount { get; init; }
    public int TasksInProgressCount { get; init; }
    public double AverageEfficiencyPercentage { get; init; }
    public double TotalHoursLoggedToday { get; init; }
    public double TotalHoursLogged => TotalHoursLoggedToday;
    public List<TimesheetRowDto> Rows { get; init; } = new();
    public List<TimesheetRowDto> DailyBreakdown { get; init; } = new();
}
