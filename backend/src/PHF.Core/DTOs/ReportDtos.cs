namespace PHF.Core.DTOs;

#region Month-End Attendance Report

public class MonthEndAttendanceReportDto
{
    public string Period { get; set; } = string.Empty; // e.g. "October 2026"
    public int Year { get; set; }
    public int Month { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int TotalCalendarDays { get; set; }
    public int TotalWorkingDays { get; set; } // Working days in month (excluding Sundays)
    public int WorkingDaysElapsed { get; set; } // Working days up to today (or end of month if in the past)
    public double AverageAttendanceRate { get; set; }
    public double TotalHoursLogged { get; set; }
    public List<AttendanceStaffSummaryDto> StaffSummaries { get; set; } = new();
    public List<AttendanceDailyLogDto> DailyAttendanceLogs { get; set; } = new();
}

public class AttendanceStaffSummaryDto
{
    public Guid UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string WorkMode { get; set; } = "REMOTE_WFH";
    public int TotalWorkingDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysAbsent { get; set; }
    public int ApprovedLeaveDays { get; set; }
    public double AttendanceRatePercentage { get; set; }
    public double TotalHours { get; set; }
    public string TotalTimeFormatted { get; set; } = "0h 0m";
    public double ActiveHours { get; set; }
    public double IdleHours { get; set; }
    public double AverageDailyHours { get; set; }
    public double EfficiencyPercentage { get; set; }
}

public class AttendanceDailyLogDto
{
    public string Date { get; set; } = string.Empty;
    public string DayOfWeek { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public string Status { get; set; } = "ABSENT"; // PRESENT, LEAVE, ABSENT, WEEKEND
    public string ClockIn { get; set; } = "—";
    public string ClockOut { get; set; } = "—";
    public double HoursWorked { get; set; }
    public string TimeFormatted { get; set; } = "0h 0m";
}

#endregion

#region Staff Performance Report

public class StaffPerformanceReportDto
{
    public string Period { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int TotalTasksAssigned { get; set; }
    public int TotalTasksCompleted { get; set; }
    public double TeamCompletionRate { get; set; }
    public double AverageFocusScore { get; set; }
    public List<StaffPerformanceRowDto> StaffPerformance { get; set; } = new();
}

public class StaffPerformanceRowDto
{
    public Guid UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "EMPLOYEE";
    public string StationId { get; set; } = string.Empty;
    public int TasksAssigned { get; set; }
    public int TasksCompleted { get; set; }
    public int TasksInProgress { get; set; }
    public int TasksPending { get; set; }
    public double TaskCompletionRate { get; set; }
    public double TotalHoursWorked { get; set; }
    public string TotalTimeFormatted { get; set; } = "0h 0m";
    public double ActiveHours { get; set; }
    public double IdleHours { get; set; }
    public double FocusEfficiencyPercentage { get; set; }
    public int DeliverablesSubmitted { get; set; }
    public int DeliverablesRequired { get; set; }
    public double DeliverableComplianceRate { get; set; }
    public string PerformanceRating { get; set; } = "Good Standing"; // Outstanding, High Performer, Good Standing, Needs Attention
}

#endregion

#region Leave Report

public class LeaveReportDto
{
    public string Period { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int TotalApplications { get; set; }
    public int TotalApprovedDays { get; set; }
    public int TotalPendingRequests { get; set; }
    public int TotalRejectedRequests { get; set; }
    public List<LeaveStaffSummaryDto> StaffSummaries { get; set; } = new();
    public List<LeaveDetailedRowDto> DetailedRecords { get; set; } = new();
}

public class LeaveStaffSummaryDto
{
    public Guid UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public int TotalRequests { get; set; }
    public int TotalApprovedDays { get; set; }
    public int CasualLeaveDays { get; set; }
    public int SickLeaveDays { get; set; }
    public int MedicalLeaveDays { get; set; }
    public int AnnualLeaveDays { get; set; }
    public int EmergencyLeaveDays { get; set; }
    public int PendingRequests { get; set; }
    public int RejectedRequests { get; set; }
}

public class LeaveDetailedRowDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string LeaveType { get; set; } = "CASUAL";
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int TotalDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string? ReviewerName { get; set; }
    public string? ReviewComments { get; set; }
    public string AppliedAt { get; set; } = string.Empty;
    public string? ReviewedAt { get; set; }
}

#endregion
