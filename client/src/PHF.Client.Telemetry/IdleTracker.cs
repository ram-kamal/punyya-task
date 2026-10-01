using PHF.Client.Telemetry.Events;
using PHF.Client.Telemetry.Win32;

namespace PHF.Client.Telemetry;

public class IdleTracker : IDisposable
{
    private readonly Timer _timer;
    private readonly int _idleThresholdSeconds;
    private bool _isCurrentlyIdle = false;
    private readonly object _lock = new();
    private DateTime? _idleStartedUtc;

    public event EventHandler<IdleStatusChangedEventArgs>? IdleStatusChanged;

    public bool IsIdle => _isCurrentlyIdle;
    public int IdleThresholdSeconds => _idleThresholdSeconds;

    public IdleTracker(int idleThresholdSeconds = 300)
    {
        _idleThresholdSeconds = idleThresholdSeconds;
        // Check every 2 seconds
        _timer = new Timer(CheckIdleState, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(2));
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void CheckIdleState(object? state)
    {
        lock (_lock)
        {
            var idleSeconds = GetIdleSeconds();
            var now = DateTime.UtcNow;

            if (idleSeconds >= _idleThresholdSeconds)
            {
                if (!_isCurrentlyIdle)
                {
                    _isCurrentlyIdle = true;
                    _idleStartedUtc = now.AddSeconds(-idleSeconds);
                    IdleStatusChanged?.Invoke(this, new IdleStatusChangedEventArgs(true, idleSeconds, now));
                }
            }
            else
            {
                if (_isCurrentlyIdle)
                {
                    _isCurrentlyIdle = false;
                    var totalIdleSec = _idleStartedUtc.HasValue ? (int)(now - _idleStartedUtc.Value).TotalSeconds : idleSeconds;
                    _idleStartedUtc = null;
                    IdleStatusChanged?.Invoke(this, new IdleStatusChangedEventArgs(false, totalIdleSec, now));
                }
            }
        }
    }

    public int GetIdleSeconds()
    {
        var lastInput = new Win32Native.LASTINPUTINFO();
        lastInput.cbSize = Win32Native.LASTINPUTINFO.SizeOf;

        if (Win32Native.GetLastInputInfo(ref lastInput))
        {
            var currentTick = (uint)Environment.TickCount;
            var elapsedMs = currentTick >= lastInput.dwTime 
                ? currentTick - lastInput.dwTime 
                : (uint.MaxValue - lastInput.dwTime) + currentTick;

            return (int)(elapsedMs / 1000);
        }

        return 0;
    }

    public void Dispose()
    {
        _timer.Dispose();
        GC.SuppressFinalize(this);
    }
}
