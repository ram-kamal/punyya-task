using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using PHF.Core.DTOs;

namespace PHF.Client.Desktop.ViewModels;

public class AdminWorkstationItemViewModel
{
    private readonly WorkstationStatusDto _dto;

    public AdminWorkstationItemViewModel(WorkstationStatusDto dto)
    {
        _dto = dto;
    }

    public string StationId => _dto.StationId;
    public string UserFullName => !string.IsNullOrWhiteSpace(_dto.FullName) ? _dto.FullName : "Unassigned Station";
    public string InitialLetter => !string.IsNullOrWhiteSpace(_dto.FullName) ? _dto.FullName[..1].ToUpper() : "?";
    public string State => _dto.State?.ToUpper() ?? "OFFLINE";
    public string WindowTitle => !string.IsNullOrWhiteSpace(_dto.WindowTitle) ? _dto.WindowTitle : "—";
    public int ActiveMinutes => _dto.ActiveMinutesToday;
    public int IdleMinutes => _dto.IdleMinutesToday;
    public string ActiveTimeText => $"{_dto.ActiveMinutesToday / 60}h {_dto.ActiveMinutesToday % 60}m";
    public string IdleText => IdleMinutes > 0 ? $"{IdleMinutes}m idle today" : "Active";
    public string? CurrentTaskCode => _dto.CurrentTaskCode;
    public string? CurrentTaskTitle => _dto.CurrentTaskTitle;
    public bool HasActiveTask => !string.IsNullOrWhiteSpace(_dto.CurrentTaskCode);
    public string TaskDisplayText => HasActiveTask ? $"{_dto.CurrentTaskCode}: {_dto.CurrentTaskTitle}" : "No active task";

    public string ActiveAppName
    {
        get
        {
            if (State == "OFFLINE") return "Offline";
            if (string.IsNullOrWhiteSpace(_dto.FocusedApp) || _dto.FocusedApp == "—")
                return State == "ACTIVE" ? "Desktop Active" : "—";

            var app = _dto.FocusedApp;
            if (app.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                app = app[..^4];
            return app;
        }
    }

    public IBrush StateDotBrush => State switch
    {
        "ACTIVE" => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
        "IDLE" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // Amber
        _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))        // Slate / Gray
    };

    public IBrush StateBgBrush => State switch
    {
        "ACTIVE" => new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
        "IDLE" => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
        _ => new SolidColorBrush(Color.FromArgb(30, 100, 116, 139))
    };

    public IBrush StateFgBrush => State switch
    {
        "ACTIVE" => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        "IDLE" => new SolidColorBrush(Color.FromRgb(252, 211, 77)),
        _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
    };

    public string LastSeenText => _dto.LastSeen.ToLocalTime().ToString("HH:mm:ss");
}

public class AdminTaskItemViewModel : INotifyPropertyChanged
{
    private readonly TaskResponseDto _dto;
    private bool _isSelected;

    public AdminTaskItemViewModel(TaskResponseDto dto)
    {
        _dto = dto;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public Guid Id => _dto.Id;
    public string TaskCode => _dto.TaskCode ?? $"TSK-{_dto.Id.ToString()[..4].ToUpper()}";
    public string Title => _dto.Title;
    public string Description => !string.IsNullOrWhiteSpace(_dto.Description) ? _dto.Description : "—";
    public string Priority => _dto.Priority ?? "MEDIUM";
    public string Status => _dto.Status ?? "ASSIGNED";
    public string AssigneeName => !string.IsNullOrWhiteSpace(_dto.AssignedToName) ? _dto.AssignedToName : "Unassigned";
    public string AssigneeInitial => !string.IsNullOrWhiteSpace(_dto.AssignedToName) ? _dto.AssignedToName[..1].ToUpper() : "?";
    public string AssigneeStation => !string.IsNullOrWhiteSpace(_dto.AssignedToStation) ? _dto.AssignedToStation : "—";
    public string TrackedTimeText => $"{Math.Round(_dto.TotalTrackedSeconds / 60.0)}m";
    public bool HasDeliverable => !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) || !string.IsNullOrWhiteSpace(_dto.DeliverableUrl) || (_dto.Attachments != null && _dto.Attachments.Count > 0);
    public string DeliverableName => !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) ? _dto.DeliverableFileName : (!string.IsNullOrWhiteSpace(_dto.DeliverableUrl) ? "Proof Link" : "None");
    public string? DeliverableUrl => _dto.DeliverableUrl;
    public bool DeliverableRequired => _dto.DeliverableRequired;
    public TaskResponseDto Dto => _dto;

    public string DeliverableStatusText => HasDeliverable
        ? $"✓ {DeliverableName}"
        : (DeliverableRequired ? "⚠️ Proof Required" : "Optional");

    public IBrush DeliverableStatusBgBrush => HasDeliverable
        ? new SolidColorBrush(Color.FromArgb(35, 16, 185, 129))
        : (DeliverableRequired
            ? new SolidColorBrush(Color.FromArgb(35, 245, 158, 11))
            : new SolidColorBrush(Color.FromArgb(20, 148, 163, 184)));

    public IBrush DeliverableStatusFgBrush => HasDeliverable
        ? new SolidColorBrush(Color.FromRgb(52, 211, 153))
        : (DeliverableRequired
            ? new SolidColorBrush(Color.FromRgb(252, 211, 77))
            : new SolidColorBrush(Color.FromRgb(148, 163, 184)));

    public IBrush PriorityBgBrush => _dto.Priority switch
    {
        "CRITICAL" => new SolidColorBrush(Color.FromArgb(40, 225, 29, 72)),
        "HIGH" => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
        "MEDIUM" => new SolidColorBrush(Color.FromArgb(35, 244, 63, 94)),
        _ => new SolidColorBrush(Color.FromArgb(30, 148, 163, 184))
    };

    public IBrush PriorityFgBrush => _dto.Priority switch
    {
        "CRITICAL" => new SolidColorBrush(Color.FromRgb(251, 113, 133)),
        "HIGH" => new SolidColorBrush(Color.FromRgb(252, 211, 77)),
        "MEDIUM" => new SolidColorBrush(Color.FromRgb(253, 164, 175)),
        _ => new SolidColorBrush(Color.FromRgb(203, 213, 225))
    };

    public IBrush StatusBgBrush => _dto.Status switch
    {
        "COMPLETED" => new SolidColorBrush(Color.FromArgb(35, 16, 185, 129)),
        "IN_PROGRESS" => new SolidColorBrush(Color.FromArgb(35, 225, 29, 72)),
        "UNDER_REVIEW" => new SolidColorBrush(Color.FromArgb(35, 14, 165, 233)),
        _ => new SolidColorBrush(Color.FromArgb(25, 148, 163, 184))
    };

    public IBrush StatusFgBrush => _dto.Status switch
    {
        "COMPLETED" => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        "IN_PROGRESS" => new SolidColorBrush(Color.FromRgb(251, 113, 133)),
        "UNDER_REVIEW" => new SolidColorBrush(Color.FromRgb(56, 189, 248)),
        _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class AdminTimesheetRowViewModel
{
    private readonly TimesheetRowDto _dto;

    public AdminTimesheetRowViewModel(TimesheetRowDto dto)
    {
        _dto = dto;
    }

    public string Date => _dto.Date;
    public string UserFullName => _dto.EmployeeName;
    public string InitialLetter => !string.IsNullOrWhiteSpace(_dto.EmployeeName) ? _dto.EmployeeName[..1].ToUpper() : "?";
    public string UserEmail => _dto.Email;
    public string StationId => _dto.StationId;
    public string ClockInText => !string.IsNullOrWhiteSpace(_dto.ClockIn) ? _dto.ClockIn : "—";
    public string ClockOutText => !string.IsNullOrWhiteSpace(_dto.ClockOut) ? _dto.ClockOut : "Active Shift";
    public string TotalHoursText => $"{_dto.TotalHours:F1}h";
    public string ActiveHoursText => $"{_dto.ActiveHours:F1}h";
    public string IdleHoursText => $"{_dto.IdleHours:F1}h";
    public string FocusScoreText => $"{_dto.EfficiencyPercentage:F0}%";
    public int TasksCompleted => _dto.TasksCompleted;
    public string TasksCompletedText => $"{_dto.TasksCompleted} tasks";

    public IBrush FocusColorBrush => _dto.EfficiencyPercentage switch
    {
        >= 80 => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        >= 50 => new SolidColorBrush(Color.FromRgb(252, 211, 77)),
        _ => new SolidColorBrush(Color.FromRgb(248, 113, 113))
    };
}

public class AdminLeaveApprovalViewModel
{
    private readonly LeaveResponseDto _dto;

    public AdminLeaveApprovalViewModel(LeaveResponseDto dto)
    {
        _dto = dto;
    }

    public Guid Id => _dto.Id;
    public string UserName => _dto.UserName;
    public string InitialLetter => !string.IsNullOrWhiteSpace(_dto.UserName) ? _dto.UserName[..1].ToUpper() : "?";
    public string UserEmail => _dto.UserEmail;
    public string StationId => !string.IsNullOrWhiteSpace(_dto.StationId) ? _dto.StationId : "—";
    public string LeaveType => _dto.LeaveType;
    public string DateRangeText => $"{_dto.StartDate:MMM dd} – {_dto.EndDate:MMM dd, yyyy}";
    public string TotalDaysText => $"{_dto.TotalDays}d";
    public string Reason => _dto.Reason;
    public string Status => _dto.Status;
    public bool IsPending => _dto.Status.Equals("PENDING", StringComparison.OrdinalIgnoreCase);

    public IBrush LeaveTypeBgBrush => _dto.LeaveType?.ToUpper() switch
    {
        "SICK" or "MEDICAL" => new SolidColorBrush(Color.FromArgb(40, 225, 29, 72)),
        "EMERGENCY" => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
        "ANNUAL" => new SolidColorBrush(Color.FromArgb(40, 99, 102, 241)),
        _ => new SolidColorBrush(Color.FromArgb(40, 14, 165, 233))
    };

    public IBrush LeaveTypeFgBrush => _dto.LeaveType?.ToUpper() switch
    {
        "SICK" or "MEDICAL" => new SolidColorBrush(Color.FromRgb(251, 113, 133)),
        "EMERGENCY" => new SolidColorBrush(Color.FromRgb(252, 211, 77)),
        "ANNUAL" => new SolidColorBrush(Color.FromRgb(165, 180, 252)),
        _ => new SolidColorBrush(Color.FromRgb(56, 189, 248))
    };

    public IBrush StatusBgBrush => _dto.Status switch
    {
        "APPROVED" => new SolidColorBrush(Color.FromArgb(40, 34, 197, 94)),
        "REJECTED" => new SolidColorBrush(Color.FromArgb(40, 239, 68, 68)),
        _ => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11))
    };

    public IBrush StatusFgBrush => _dto.Status switch
    {
        "APPROVED" => new SolidColorBrush(Color.FromRgb(110, 231, 183)),
        "REJECTED" => new SolidColorBrush(Color.FromRgb(252, 165, 165)),
        _ => new SolidColorBrush(Color.FromRgb(252, 211, 77))
    };

    public string ReviewInfo => !string.IsNullOrWhiteSpace(_dto.ReviewerName)
        ? $"Reviewed by {_dto.ReviewerName}: {_dto.ReviewComments}"
        : "Pending Supervisor Review";
}

public class AdminStaffMemberViewModel
{
    private readonly StaffMemberDto _dto;

    public AdminStaffMemberViewModel(StaffMemberDto dto)
    {
        _dto = dto;
    }

    public Guid Id => _dto.Id;
    public string FullName => _dto.FullName;
    public string InitialLetter => !string.IsNullOrWhiteSpace(_dto.FullName) ? _dto.FullName[..1].ToUpper() : "?";
    public string Username => _dto.Username ?? "—";
    public string Email => _dto.Email;
    public string Role => _dto.Role;
    public string WorkMode => _dto.WorkMode;
    public string WorkModeDisplay => WorkMode == "REMOTE_WFH" ? "Remote WFH" : "In-Office";

    public IBrush WorkModeBgBrush => WorkMode == "REMOTE_WFH"
        ? new SolidColorBrush(Color.FromArgb(35, 6, 182, 212))
        : new SolidColorBrush(Color.FromArgb(35, 99, 102, 241));

    public IBrush WorkModeFgBrush => WorkMode == "REMOTE_WFH"
        ? new SolidColorBrush(Color.FromRgb(34, 211, 238))
        : new SolidColorBrush(Color.FromRgb(165, 180, 252));

    public string StationId => !string.IsNullOrWhiteSpace(_dto.StationId) ? _dto.StationId : "—";
    public bool IsActive => _dto.IsActive;
    public int ActiveTasksCount => _dto.ActiveTasksCount;
    public string ShiftStatus => _dto.ShiftStatus ?? "OFFLINE";

    public IBrush RoleBadgeBrush => _dto.Role switch
    {
        "ADMIN" => new SolidColorBrush(Color.FromArgb(50, 225, 29, 72)),
        "MANAGER" => new SolidColorBrush(Color.FromArgb(50, 99, 102, 241)),
        _ => new SolidColorBrush(Color.FromArgb(35, 148, 163, 184))
    };

    public IBrush RoleFgBrush => _dto.Role switch
    {
        "ADMIN" => new SolidColorBrush(Color.FromRgb(251, 113, 133)),
        "MANAGER" => new SolidColorBrush(Color.FromRgb(165, 180, 252)),
        _ => new SolidColorBrush(Color.FromRgb(203, 213, 225))
    };

    public IBrush ShiftStatusBrush => ShiftStatus switch
    {
        "CLOCKED_IN" => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
    };

    public StaffMemberDto Dto => _dto;
    public string ActiveStatusText => _dto.IsActive ? "ACTIVE" : "SUSPENDED";
    public IBrush ActiveStatusBgBrush => _dto.IsActive
        ? new SolidColorBrush(Color.FromArgb(40, 16, 185, 129))
        : new SolidColorBrush(Color.FromArgb(40, 239, 68, 68));
    public IBrush ActiveStatusFgBrush => _dto.IsActive
        ? new SolidColorBrush(Color.FromRgb(52, 211, 153))
        : new SolidColorBrush(Color.FromRgb(248, 113, 113));
    public string ToggleStatusButtonText => _dto.IsActive ? "⏸ Suspend" : "▶ Activate";
}

public class AdminAttendanceSummaryViewModel
{
    private readonly AttendanceStaffSummaryDto _dto;

    public AdminAttendanceSummaryViewModel(AttendanceStaffSummaryDto dto)
    {
        _dto = dto;
    }

    public Guid UserId => _dto.UserId;
    public string EmployeeName => _dto.EmployeeName;
    public string Email => _dto.Email;
    public string StationId => !string.IsNullOrWhiteSpace(_dto.StationId) ? _dto.StationId : "—";
    public string WorkMode => _dto.WorkMode;
    public string WorkModeDisplay => _dto.WorkMode == "IN_OFFICE" ? "Office" : "WFH";
    public int TotalWorkingDays => _dto.TotalWorkingDays;
    public int DaysPresent => _dto.DaysPresent;
    public int DaysAbsent => _dto.DaysAbsent;
    public int ApprovedLeaveDays => _dto.ApprovedLeaveDays;
    public double AttendanceRatePercentage => _dto.AttendanceRatePercentage;
    public string AttendanceRateText => $"{_dto.AttendanceRatePercentage:F1}%";
    public double TotalHours => _dto.TotalHours;
    public string TotalTimeFormatted => _dto.TotalTimeFormatted;
    public double ActiveHours => _dto.ActiveHours;
    public double IdleHours => _dto.IdleHours;
    public string ActiveIdleText => $"{_dto.ActiveHours:F1}h / {_dto.IdleHours:F1}h";
    public double AverageDailyHours => _dto.AverageDailyHours;
    public string AvgDailyText => $"{_dto.AverageDailyHours:F1}h/day";
    public double EfficiencyPercentage => _dto.EfficiencyPercentage;
    public string EfficiencyText => $"{_dto.EfficiencyPercentage:F1}%";

    public IBrush RateColorBrush => AttendanceRatePercentage switch
    {
        >= 90 => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        >= 75 => new SolidColorBrush(Color.FromRgb(56, 189, 248)),
        >= 60 => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
        _ => new SolidColorBrush(Color.FromRgb(251, 113, 133))
    };

    public IBrush EfficiencyBrush => EfficiencyPercentage switch
    {
        >= 80 => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        >= 60 => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
        _ => new SolidColorBrush(Color.FromRgb(251, 113, 133))
    };
}

public class AdminAttendanceDailyLogViewModel
{
    private readonly AttendanceDailyLogDto _dto;

    public AdminAttendanceDailyLogViewModel(AttendanceDailyLogDto dto)
    {
        _dto = dto;
    }

    public string Date => _dto.Date;
    public string DayOfWeek => _dto.DayOfWeek;
    public string EmployeeName => _dto.EmployeeName;
    public string Email => _dto.Email;
    public string StationId => !string.IsNullOrWhiteSpace(_dto.StationId) ? _dto.StationId : "—";
    public string Status => _dto.Status;
    public string ClockIn => _dto.ClockIn;
    public string ClockOut => _dto.ClockOut;
    public double HoursWorked => _dto.HoursWorked;
    public string TimeFormatted => _dto.TimeFormatted;

    public IBrush StatusBrush => _dto.Status switch
    {
        "PRESENT" => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        "LEAVE" => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
        "WEEKEND" => new SolidColorBrush(Color.FromRgb(96, 165, 250)),
        _ => new SolidColorBrush(Color.FromRgb(251, 113, 133))
    };

    public IBrush StatusBgBrush => _dto.Status switch
    {
        "PRESENT" => new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
        "LEAVE" => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
        "WEEKEND" => new SolidColorBrush(Color.FromArgb(40, 59, 130, 246)),
        _ => new SolidColorBrush(Color.FromArgb(40, 239, 68, 68))
    };
}

public class AdminStaffPerformanceRowViewModel
{
    private readonly StaffPerformanceRowDto _dto;

    public AdminStaffPerformanceRowViewModel(StaffPerformanceRowDto dto)
    {
        _dto = dto;
    }

    public Guid UserId => _dto.UserId;
    public string EmployeeName => _dto.EmployeeName;
    public string Email => _dto.Email;
    public string Role => _dto.Role;
    public string StationId => !string.IsNullOrWhiteSpace(_dto.StationId) ? _dto.StationId : "—";
    public int TasksAssigned => _dto.TasksAssigned;
    public int TasksCompleted => _dto.TasksCompleted;
    public int TasksInProgress => _dto.TasksInProgress;
    public int TasksPending => _dto.TasksPending;
    public double TaskCompletionRate => _dto.TaskCompletionRate;
    public string CompletionRateText => $"{_dto.TaskCompletionRate:F1}%";
    public int DeliverablesSubmitted => _dto.DeliverablesSubmitted;
    public int DeliverablesRequired => _dto.DeliverablesRequired;
    public double DeliverableComplianceRate => _dto.DeliverableComplianceRate;
    public string ComplianceText => $"{_dto.DeliverablesSubmitted}/{_dto.DeliverablesRequired} ({_dto.DeliverableComplianceRate:F0}%)";
    public double TotalHoursWorked => _dto.TotalHoursWorked;
    public string TotalTimeFormatted => _dto.TotalTimeFormatted;
    public double ActiveHours => _dto.ActiveHours;
    public double IdleHours => _dto.IdleHours;
    public double FocusEfficiencyPercentage => _dto.FocusEfficiencyPercentage;
    public string FocusText => $"{_dto.FocusEfficiencyPercentage:F1}%";
    public string PerformanceRating => _dto.PerformanceRating;

    public IBrush RatingBgBrush => _dto.PerformanceRating switch
    {
        "Outstanding" => new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
        "High Performer" => new SolidColorBrush(Color.FromArgb(40, 56, 189, 248)),
        "Good Standing" => new SolidColorBrush(Color.FromArgb(40, 245, 158, 11)),
        _ => new SolidColorBrush(Color.FromArgb(40, 239, 68, 68))
    };

    public IBrush RatingFgBrush => _dto.PerformanceRating switch
    {
        "Outstanding" => new SolidColorBrush(Color.FromRgb(52, 211, 153)),
        "High Performer" => new SolidColorBrush(Color.FromRgb(56, 189, 248)),
        "Good Standing" => new SolidColorBrush(Color.FromRgb(251, 191, 36)),
        _ => new SolidColorBrush(Color.FromRgb(251, 113, 133))
    };
}

public class AdminLeaveStaffSummaryViewModel
{
    private readonly LeaveStaffSummaryDto _dto;

    public AdminLeaveStaffSummaryViewModel(LeaveStaffSummaryDto dto)
    {
        _dto = dto;
    }

    public Guid UserId => _dto.UserId;
    public string EmployeeName => _dto.EmployeeName;
    public string Email => _dto.Email;
    public string StationId => !string.IsNullOrWhiteSpace(_dto.StationId) ? _dto.StationId : "—";
    public int TotalRequests => _dto.TotalRequests;
    public int TotalApprovedDays => _dto.TotalApprovedDays;
    public int CasualLeaveDays => _dto.CasualLeaveDays;
    public int SickLeaveDays => _dto.SickLeaveDays;
    public int MedicalLeaveDays => _dto.MedicalLeaveDays;
    public int AnnualLeaveDays => _dto.AnnualLeaveDays;
    public int EmergencyLeaveDays => _dto.EmergencyLeaveDays;
    public int PendingRequests => _dto.PendingRequests;
    public int RejectedRequests => _dto.RejectedRequests;
}

