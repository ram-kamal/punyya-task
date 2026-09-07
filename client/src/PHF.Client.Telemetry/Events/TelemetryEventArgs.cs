namespace PHF.Client.Telemetry.Events;

public class WindowFocusChangedEventArgs : EventArgs
{
    public string ProcessName { get; }
    public string WindowTitle { get; }
    public DateTime Timestamp { get; }
    public int DurationPreviousSeconds { get; }

    public WindowFocusChangedEventArgs(string processName, string windowTitle, DateTime timestamp, int durationPreviousSeconds)
    {
        ProcessName = processName;
        WindowTitle = windowTitle;
        Timestamp = timestamp;
        DurationPreviousSeconds = durationPreviousSeconds;
    }
}

public class IdleStatusChangedEventArgs : EventArgs
{
    public bool IsIdle { get; }
    public int IdleSeconds { get; }
    public DateTime Timestamp { get; }

    public IdleStatusChangedEventArgs(bool isIdle, int idleSeconds, DateTime timestamp)
    {
        IsIdle = isIdle;
        IdleSeconds = idleSeconds;
        Timestamp = timestamp;
    }
}

public class SessionStatusChangedEventArgs : EventArgs
{
    public string EventType { get; } // LOGON, LOGOFF, LOCK, UNLOCK
    public string MachineName { get; }
    public DateTime Timestamp { get; }

    public SessionStatusChangedEventArgs(string eventType, string machineName, DateTime timestamp)
    {
        EventType = eventType;
        MachineName = machineName;
        Timestamp = timestamp;
    }
}
