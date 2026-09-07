using Microsoft.Win32;
using PHF.Client.Telemetry.Events;

namespace PHF.Client.Telemetry;

public class SessionTracker : IDisposable
{
    public event EventHandler<SessionStatusChangedEventArgs>? SessionStatusChanged;
    private readonly string _machineName;

    public SessionTracker()
    {
        _machineName = Environment.MachineName;
    }

    public void Start()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    public void Stop()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        var now = DateTime.UtcNow;
        var eventType = e.Reason switch
        {
            SessionSwitchReason.SessionLock => "LOCK",
            SessionSwitchReason.SessionUnlock => "UNLOCK",
            SessionSwitchReason.SessionLogon or SessionSwitchReason.ConsoleConnect => "LOGON",
            SessionSwitchReason.SessionLogoff or SessionSwitchReason.ConsoleDisconnect => "LOGOFF",
            _ => e.Reason.ToString().ToUpperInvariant()
        };

        SessionStatusChanged?.Invoke(this, new SessionStatusChangedEventArgs(eventType, _machineName, now));
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
