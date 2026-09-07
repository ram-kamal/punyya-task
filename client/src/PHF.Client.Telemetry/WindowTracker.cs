using System.Diagnostics;
using System.Text;
using PHF.Client.Telemetry.Events;
using PHF.Client.Telemetry.Win32;

namespace PHF.Client.Telemetry;

public class WindowTracker : IDisposable
{
    private IntPtr _hookHandle = IntPtr.Zero;
    private Win32Native.WinEventDelegate? _eventDelegate;
    private readonly object _lock = new();

    private string _currentProcessName = "Desktop";
    private string _currentWindowTitle = "Windows Desktop";
    private DateTime _lastFocusChange = DateTime.UtcNow;

    public event EventHandler<WindowFocusChangedEventArgs>? WindowFocusChanged;

    public string CurrentProcessName => _currentProcessName;
    public string CurrentWindowTitle => _currentWindowTitle;

    public void Start()
    {
        lock (_lock)
        {
            if (_hookHandle != IntPtr.Zero) return;

            // Must maintain GC reference to delegate
            _eventDelegate = OnWinEventForeground;

            _hookHandle = Win32Native.SetWinEventHook(
                Win32Native.EVENT_SYSTEM_FOREGROUND,
                Win32Native.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _eventDelegate,
                0,
                0,
                Win32Native.WINEVENT_OUTOFCONTEXT | Win32Native.WINEVENT_SKIPOWNPROCESS);

            _lastFocusChange = DateTime.UtcNow;

            // Capture initial window
            CaptureCurrentWindow();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_hookHandle != IntPtr.Zero)
            {
                Win32Native.UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _eventDelegate = null;
            }
        }
    }

    private void OnWinEventForeground(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (eventType != Win32Native.EVENT_SYSTEM_FOREGROUND || hwnd == IntPtr.Zero)
            return;

        ProcessForegroundChange(hwnd);
    }

    private void ProcessForegroundChange(IntPtr hwnd)
    {
        try
        {
            var now = DateTime.UtcNow;
            var durationSeconds = (int)(now - _lastFocusChange).TotalSeconds;
            if (durationSeconds < 0) durationSeconds = 0;

            var prevProcess = _currentProcessName;
            var prevTitle = _currentWindowTitle;

            // Retrieve Process Name
            var processName = "Unknown";
            Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid > 0)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    processName = proc.ProcessName;
                }
                catch
                {
                    processName = "System";
                }
            }

            // Retrieve Window Title
            var titleBuilder = new StringBuilder(512);
            Win32Native.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
            var windowTitle = titleBuilder.ToString();

            if (string.IsNullOrWhiteSpace(windowTitle))
            {
                windowTitle = processName;
            }

            // Fire focus change event with previous application duration
            WindowFocusChanged?.Invoke(this, new WindowFocusChangedEventArgs(
                prevProcess,
                prevTitle,
                now,
                durationSeconds));

            _currentProcessName = processName;
            _currentWindowTitle = windowTitle;
            _lastFocusChange = now;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowTracker] Error tracking foreground: {ex.Message}");
        }
    }

    public void CaptureCurrentWindow()
    {
        var hwnd = Win32Native.GetForegroundWindow();
        if (hwnd != IntPtr.Zero)
        {
            ProcessForegroundChange(hwnd);
        }
    }

    public (string ProcessName, string WindowTitle, int DurationSeconds) FlushCurrentSegment()
    {
        var now = DateTime.UtcNow;
        var duration = (int)(now - _lastFocusChange).TotalSeconds;
        _lastFocusChange = now;
        return (_currentProcessName, _currentWindowTitle, Math.Max(1, duration));
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
