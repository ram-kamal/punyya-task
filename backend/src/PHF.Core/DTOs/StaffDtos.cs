namespace PHF.Core.DTOs;

public record StaffMemberDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string WorkMode { get; init; } = "IN_OFFICE";
    public string? StationId { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTime CreatedAt { get; init; }
    public int ActiveTasksCount { get; init; }
    public string? ShiftStatus { get; init; }
}

public record CreateStaffRequest
{
    public string FullName { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = "EMPLOYEE";
    public string WorkMode { get; init; } = "IN_OFFICE";
    public string? StationId { get; init; }
    public string Password { get; init; } = string.Empty;
}

public record ResetPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
}

public record UpdateWorkModeRequest
{
    public string WorkMode { get; init; } = "IN_OFFICE"; // "IN_OFFICE" or "REMOTE_WFH"
}
