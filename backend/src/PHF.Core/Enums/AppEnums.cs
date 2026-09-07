namespace PHF.Core.Enums;

public enum UserRole
{
    ADMIN,
    MANAGER,
    EMPLOYEE
}

public enum TaskItemStatus
{
    ASSIGNED,
    IN_PROGRESS,
    UNDER_REVIEW,
    COMPLETED
}

public enum TaskPriority
{
    LOW,
    MEDIUM,
    HIGH,
    CRITICAL
}

public enum SessionEventType
{
    LOGON,
    LOGOFF,
    LOCK,
    UNLOCK
}

public enum StationState
{
    ACTIVE,
    IDLE,
    OFFLINE
}

public enum ShiftStatus
{
    ACTIVE,
    COMPLETED
}
