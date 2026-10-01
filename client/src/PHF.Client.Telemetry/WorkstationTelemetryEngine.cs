using PHF.Client.Core.Telemetry;
using PHF.Client.Telemetry.Events;

namespace PHF.Client.Telemetry;

public class WorkstationTelemetryEngine : ITelemetryEngine
{
    private readonly WindowTracker _windowTracker;
    private readonly IdleTracker _idleTracker;
    private readonly SessionTracker _sessionTracker;

    private Guid? _currentTaskId;
    private string? _currentTaskCode;

    public event Action<string, string, int, bool, Guid?, string?, DateTime>? ActivityLogged;
    public event Action<string, string, DateTime>? SessionLogged;
    public event Action<bool, int>? IdleStateChanged;
    public event Action<string, string>? FocusChanged;

    public string CurrentProcessName => _windowTracker.CurrentProcessName;
    public string CurrentWindowTitle => _windowTracker.CurrentWindowTitle;
    public bool IsIdle => _idleTracker.IsIdle;
    public int IdleSeconds => _idleTracker.GetIdleSeconds();
    public Guid? CurrentTaskId => _currentTaskId;
    public string? CurrentTaskCode => _currentTaskCode;

    public WorkstationTelemetryEngine(int idleThresholdSeconds = 300)
    {
        _windowTracker = new WindowTracker();
        _idleTracker = new IdleTracker(idleThresholdSeconds);
        _sessionTracker = new SessionTracker();

        _windowTracker.WindowFocusChanged += OnWindowFocusChanged;
        _idleTracker.IdleStatusChanged += OnIdleStatusChanged;
        _sessionTracker.SessionStatusChanged += OnSessionStatusChanged;
    }

    public void Start()
    {
        _windowTracker.Start();
        _idleTracker.Start();
        _sessionTracker.Start();
    }

    public void Stop()
    {
        _windowTracker.Stop();
        _idleTracker.Stop();
        _sessionTracker.Stop();
    }

    public void SetCurrentTask(Guid? taskId, string? taskCode)
    {
        _currentTaskId = taskId;
        _currentTaskCode = taskCode;
    }

    private void OnWindowFocusChanged(object? sender, WindowFocusChangedEventArgs e)
    {
        // Emit log for the previous window focus period
        if (e.DurationPreviousSeconds > 0 && !string.IsNullOrWhiteSpace(e.ProcessName))
        {
            ActivityLogged?.Invoke(
                e.ProcessName,
                e.WindowTitle,
                e.DurationPreviousSeconds,
                _idleTracker.IsIdle,
                _currentTaskId,
                _currentTaskCode,
                e.Timestamp);
        }

        FocusChanged?.Invoke(_windowTracker.CurrentProcessName, _windowTracker.CurrentWindowTitle);
    }

    private void OnIdleStatusChanged(object? sender, IdleStatusChangedEventArgs e)
    {
        IdleStateChanged?.Invoke(e.IsIdle, e.IdleSeconds);

        if (e.IsIdle)
        {
            // User reached idle threshold (e.IdleSeconds, e.g. 300s)
            // Flush current window segment and subtract the idle threshold duration so active app doesn't claim idle time
            var (process, title, duration) = _windowTracker.FlushCurrentSegment();
            var activeDuration = Math.Max(0, duration - e.IdleSeconds);
            if (activeDuration > 0 && !string.IsNullOrWhiteSpace(process))
            {
                ActivityLogged?.Invoke(
                    process,
                    title,
                    activeDuration,
                    false,
                    _currentTaskId,
                    _currentTaskCode,
                    e.Timestamp.AddSeconds(-e.IdleSeconds));
            }

            // Log the initial idle period
            ActivityLogged?.Invoke(
                "System Idle",
                "Idle Wait State",
                e.IdleSeconds,
                true,
                _currentTaskId,
                _currentTaskCode,
                e.Timestamp);
        }
        else
        {
            // User resumed activity after being away!
            // Log remaining idle duration beyond the initial threshold
            var remainingIdle = Math.Max(0, e.IdleSeconds - _idleTracker.IdleThresholdSeconds);
            if (remainingIdle > 0)
            {
                ActivityLogged?.Invoke(
                    "System Idle",
                    "Idle Wait State",
                    remainingIdle,
                    true,
                    _currentTaskId,
                    _currentTaskCode,
                    e.Timestamp);
            }

            // Reset focus segment so the active app starts fresh from right now
            _windowTracker.FlushCurrentSegment();
        }
    }

    private void OnSessionStatusChanged(object? sender, SessionStatusChangedEventArgs e)
    {
        SessionLogged?.Invoke(e.EventType, e.MachineName, e.Timestamp);
    }

    public void FlushCurrentActivity()
    {
        var (process, title, duration) = _windowTracker.FlushCurrentSegment();
        if (duration > 0 && !string.IsNullOrWhiteSpace(process))
        {
            ActivityLogged?.Invoke(
                process,
                title,
                duration,
                _idleTracker.IsIdle,
                _currentTaskId,
                _currentTaskCode,
                DateTime.UtcNow);
        }
    }

    public void Dispose()
    {
        _windowTracker.WindowFocusChanged -= OnWindowFocusChanged;
        _idleTracker.IdleStatusChanged -= OnIdleStatusChanged;
        _sessionTracker.SessionStatusChanged -= OnSessionStatusChanged;

        _windowTracker.Dispose();
        _idleTracker.Dispose();
        _sessionTracker.Dispose();
        GC.SuppressFinalize(this);
    }
}
