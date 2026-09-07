using System.Diagnostics;
using System.Text;
using PHF.Client.Telemetry.Events;
using PHF.Client.Telemetry.Win32;

namespace PHF.Client.Telemetry;

public class WindowTracker : IDisposable
{
    private IntPtr _hookHandle = IntPtr.Zero;
    private IntPtr _lastHwnd = IntPtr.Zero;
    private Win32Native.WinEventDelegate? _eventDelegate;
    private readonly Timer _pollTimer;
    private readonly object _lock = new();

    private string _currentProcessName = "Desktop";
    private string _currentWindowTitle = "Windows Desktop";
    private DateTime _lastFocusChange = DateTime.UtcNow;

    public event EventHandler<WindowFocusChangedEventArgs>? WindowFocusChanged;

    public string CurrentProcessName => _currentProcessName;
    public string CurrentWindowTitle => _currentWindowTitle;

    public WindowTracker()
    {
        // Periodic 1-second fallback polling to ensure zero missed focus changes
        _pollTimer = new Timer(_ => CheckForegroundPoll(), null, Timeout.Infinite, Timeout.Infinite);
    }

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
                Win32Native.WINEVENT_OUTOFCONTEXT);

            _lastFocusChange = DateTime.UtcNow;

            // Capture initial window and start fallback poller
            CaptureCurrentWindow();
            _pollTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (_hookHandle != IntPtr.Zero)
            {
                Win32Native.UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _eventDelegate = null;
            }
        }
    }

    private void CheckForegroundPoll()
    {
        try
        {
            var hwnd = Win32Native.GetForegroundWindow();
            if (hwnd != IntPtr.Zero && hwnd != _lastHwnd)
            {
                ProcessForegroundChange(hwnd);
            }
        }
        catch
        {
            // Ignore polling errors
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
        lock (_lock)
        {
            if (hwnd == _lastHwnd) return;

            try
            {
                var now = DateTime.UtcNow;
                var durationSeconds = (int)(now - _lastFocusChange).TotalSeconds;
                if (durationSeconds < 0) durationSeconds = 0;

                var prevProcess = _currentProcessName;
                var prevTitle = _currentWindowTitle;

                // Retrieve Process Name
                var rawProcessName = "Unknown";
                Win32Native.GetWindowThreadProcessId(hwnd, out var pid);
                if (pid > 0)
                {
                    try
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        rawProcessName = proc.ProcessName;
                    }
                    catch
                    {
                        rawProcessName = "System";
                    }
                }

                // Retrieve Window Title
                var titleBuilder = new StringBuilder(512);
                Win32Native.GetWindowText(hwnd, titleBuilder, titleBuilder.Capacity);
                var windowTitle = titleBuilder.ToString();

                var friendlyName = FormatFriendlyName(rawProcessName, windowTitle);

                // Fire focus change event with previous application duration
                if (!string.IsNullOrWhiteSpace(prevProcess))
                {
                    WindowFocusChanged?.Invoke(this, new WindowFocusChangedEventArgs(
                        prevProcess,
                        prevTitle,
                        now,
                        durationSeconds));
                }

                _currentProcessName = friendlyName;
                _currentWindowTitle = !string.IsNullOrWhiteSpace(windowTitle) ? windowTitle : friendlyName;
                _lastHwnd = hwnd;
                _lastFocusChange = now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowTracker] Error tracking foreground: {ex.Message}");
            }
        }
    }

    private static string FormatFriendlyName(string process, string title)
    {
        var p = process.ToLowerInvariant();
        return p switch
        {
            "explorer" => string.IsNullOrWhiteSpace(title) || title.Equals("Program Manager", StringComparison.OrdinalIgnoreCase)
                ? "Windows Desktop"
                : (title.Equals("File Explorer", StringComparison.OrdinalIgnoreCase) ? "File Explorer" : $"File Explorer ({title})"),
            "chrome" => "Google Chrome",
            "msedge" => "Microsoft Edge",
            "firefox" => "Mozilla Firefox",
            "code" => "Visual Studio Code",
            "devenv" => "Visual Studio",
            "notepad" => "Notepad",
            "slack" => "Slack",
            "teams" => "Microsoft Teams",
            "premiere" or "adobe premiere pro" => "Adobe Premiere Pro",
            "audacity" => "Audacity",
            "windowsterminal" or "powershell" or "cmd" => "Terminal",
            _ => process.Length > 0 ? char.ToUpper(process[0]) + process.Substring(1) : process
        };
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
        _pollTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
