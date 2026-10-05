using System;
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
    public string State => _dto.State?.ToUpper() ?? "OFFLINE";
    public string WindowTitle => !string.IsNullOrWhiteSpace(_dto.WindowTitle) ? _dto.WindowTitle : "—";
    public int ActiveMinutes => _dto.ActiveMinutesToday;
    public int IdleMinutes => _dto.IdleMinutesToday;
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

public class AdminTaskItemViewModel
{
    private readonly TaskResponseDto _dto;

    public AdminTaskItemViewModel(TaskResponseDto dto)
    {
        _dto = dto;
    }

    public Guid Id => _dto.Id;
    public string TaskCode => _dto.TaskCode ?? $"TSK-{_dto.Id.ToString()[..4].ToUpper()}";
    public string Title => _dto.Title;
    public string Description => !string.IsNullOrWhiteSpace(_dto.Description) ? _dto.Description : "—";
    public string Priority => _dto.Priority ?? "MEDIUM";
    public string Status => _dto.Status ?? "ASSIGNED";
    public string AssigneeName => !string.IsNullOrWhiteSpace(_dto.AssignedToName) ? _dto.AssignedToName : "Unassigned";
    public string AssigneeStation => !string.IsNullOrWhiteSpace(_dto.AssignedToStation) ? _dto.AssignedToStation : "—";
    public string TrackedTimeText => $"{Math.Round(_dto.TotalTrackedSeconds / 60.0)}m";
    public bool HasDeliverable => !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) || !string.IsNullOrWhiteSpace(_dto.DeliverableUrl) || (_dto.Attachments != null && _dto.Attachments.Count > 0);
    public string DeliverableName => !string.IsNullOrWhiteSpace(_dto.DeliverableFileName) ? _dto.DeliverableFileName : (!string.IsNullOrWhiteSpace(_dto.DeliverableUrl) ? "Proof Link" : "None");
    public string? DeliverableUrl => _dto.DeliverableUrl;
    public bool DeliverableRequired => _dto.DeliverableRequired;
    public TaskResponseDto Dto => _dto;

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
    public string UserEmail => _dto.UserEmail;
    public string LeaveType => _dto.LeaveType;
    public string DateRangeText => $"{_dto.StartDate:MMM dd} – {_dto.EndDate:MMM dd, yyyy}";
    public string TotalDaysText => $"{_dto.TotalDays}d";
    public string Reason => _dto.Reason;
    public string Status => _dto.Status;
    public bool IsPending => _dto.Status.Equals("PENDING", StringComparison.OrdinalIgnoreCase);

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
    public string Username => _dto.Username ?? "—";
    public string Email => _dto.Email;
    public string Role => _dto.Role;
    public string WorkMode => _dto.WorkMode;
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
