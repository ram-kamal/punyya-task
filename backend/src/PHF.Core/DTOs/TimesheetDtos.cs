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
    public double EfficiencyPercentage { get; init; }
    public int TasksCompleted { get; init; }
}

public record TimesheetSummaryDto
{
    public int ActiveStationsCount { get; init; }
    public int TotalStationsCount { get; init; }
    public int TasksInProgressCount { get; init; }
    public double AverageEfficiencyPercentage { get; init; }
    public double TotalHoursLoggedToday { get; init; }
    public List<TimesheetRowDto> Rows { get; init; } = new();
}
