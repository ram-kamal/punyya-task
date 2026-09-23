using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using PHF.Client.Core.Telemetry;

namespace PHF.Client.Desktop.Telemetry;

public class MacOsTelemetryEngine : ITelemetryEngine
{
    private readonly int _idleThresholdSeconds;
    private readonly Timer _pollTimer;
    private readonly object _lock = new();

    private bool _isIdle = false;
    private int _idleSeconds = 0;
    private string _currentProcessName = "Finder";
    private string _currentWindowTitle = "Desktop";
    private DateTime _lastFocusChange = DateTime.UtcNow;

    private Guid? _currentTaskId;
    private string? _currentTaskCode;

    public event Action<string, string, int, bool, Guid?, string?, DateTime>? ActivityLogged;
#pragma warning disable CS0067
    public event Action<string, string, DateTime>? SessionLogged;
#pragma warning restore CS0067
    public event Action<bool, int>? IdleStateChanged;
    public event Action<string, string>? FocusChanged;

    public string CurrentProcessName => _currentProcessName;
    public string CurrentWindowTitle => _currentWindowTitle;
    public bool IsIdle => _isIdle;
    public int IdleSeconds => _idleSeconds;
    public Guid? CurrentTaskId => _currentTaskId;
    public string? CurrentTaskCode => _currentTaskCode;

    private const int kCGEventSourceStateCombinedSessionState = 1;
    private const uint kCGAnyInputEventType = ~0u;

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics", EntryPoint = "CGEventSourceSecondsSinceLastEventType")]
    private static extern double Mac_CGEventSourceSecondsSinceLastEventType(int sourceStateId, uint eventType);

    // Objective-C runtime P/Invokes
    [DllImport("libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr ObjC_GetClass(string name);

    [DllImport("libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr ObjC_SelRegisterName(string name);

    [DllImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjC_MsgSend(IntPtr receiver, IntPtr selector);

    [DllImport("libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr ObjC_MsgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    public MacOsTelemetryEngine(int idleThresholdSeconds = 300)
    {
        _idleThresholdSeconds = idleThresholdSeconds;
        _pollTimer = new Timer(PollTelemetryTick, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        lock (_lock)
        {
            _lastFocusChange = DateTime.UtcNow;
            _pollTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _pollTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void SetCurrentTask(Guid? taskId, string? taskCode)
    {
        _currentTaskId = taskId;
        _currentTaskCode = taskCode;
    }

    private void PollTelemetryTick(object? state)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Fallback for running on Windows or non-macOS during testing
            _idleSeconds = 0;
            _currentProcessName = "Desktop Client";
            _currentWindowTitle = "Punnya Heart Desk";
            FocusChanged?.Invoke(_currentProcessName, _currentWindowTitle);
            return;
        }

        try
        {
            // 1. Idle Detection via CoreGraphics
            double seconds = 0;
            try
            {
                seconds = Mac_CGEventSourceSecondsSinceLastEventType(kCGEventSourceStateCombinedSessionState, kCGAnyInputEventType);
            }
            catch
            {
                seconds = 0;
            }

            int idleSec = (int)Math.Max(0, seconds);
            _idleSeconds = idleSec;

            bool wasIdle = _isIdle;
            bool nowIdle = idleSec >= _idleThresholdSeconds;

            if (nowIdle != wasIdle)
            {
                _isIdle = nowIdle;
                var now = DateTime.UtcNow;
                IdleStateChanged?.Invoke(nowIdle, idleSec);

                if (nowIdle)
                {
                    ActivityLogged?.Invoke(
                        "macOS Idle",
                        "Away from Mac",
                        idleSec,
                        true,
                        _currentTaskId,
                        _currentTaskCode,
                        now);
                }
            }

            // 2. Frontmost Application via Cocoa NSWorkspace
            var (appName, windowTitle) = GetMacFrontmostApp();
            if (string.IsNullOrWhiteSpace(appName))
            {
                appName = "macOS";
            }
            if (string.IsNullOrWhiteSpace(windowTitle))
            {
                windowTitle = appName;
            }

            if (appName != _currentProcessName || windowTitle != _currentWindowTitle)
            {
                var now = DateTime.UtcNow;
                var duration = (int)(now - _lastFocusChange).TotalSeconds;
                if (duration > 0 && !string.IsNullOrWhiteSpace(_currentProcessName))
                {
                    ActivityLogged?.Invoke(
                        _currentProcessName,
                        _currentWindowTitle,
                        duration,
                        _isIdle,
                        _currentTaskId,
                        _currentTaskCode,
                        now);
                }

                _currentProcessName = appName;
                _currentWindowTitle = windowTitle;
                _lastFocusChange = now;

                FocusChanged?.Invoke(appName, windowTitle);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MacOsTelemetry] Poll error: {ex.Message}");
        }
    }

    private (string AppName, string WindowTitle) GetMacFrontmostApp()
    {
        try
        {
            // Objective-C runtime query: [NSWorkspace sharedWorkspace].frontmostApplication.localizedName
            var clsWorkspace = ObjC_GetClass("NSWorkspace");
            if (clsWorkspace != IntPtr.Zero)
            {
                var selShared = ObjC_SelRegisterName("sharedWorkspace");
                var workspace = ObjC_MsgSend(clsWorkspace, selShared);

                if (workspace != IntPtr.Zero)
                {
                    var selFrontmost = ObjC_SelRegisterName("frontmostApplication");
                    var app = ObjC_MsgSend(workspace, selFrontmost);

                    if (app != IntPtr.Zero)
                    {
                        var selName = ObjC_SelRegisterName("localizedName");
                        var nameNsString = ObjC_MsgSend(app, selName);

                        if (nameNsString != IntPtr.Zero)
                        {
                            var selUtf8 = ObjC_SelRegisterName("UTF8String");
                            var utf8Ptr = ObjC_MsgSend(nameNsString, selUtf8);

                            if (utf8Ptr != IntPtr.Zero)
                            {
                                var appName = Marshal.PtrToStringUTF8(utf8Ptr);
                                if (!string.IsNullOrWhiteSpace(appName))
                                {
                                    return (CleanMacAppName(appName), appName);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore ObjC errors and fall through
        }

        return ("Finder", "Desktop");
    }

    private static string CleanMacAppName(string name)
    {
        return name switch
        {
            "Google Chrome" => "Google Chrome",
            "Safari" => "Apple Safari",
            "Code" => "Visual Studio Code",
            "Slack" => "Slack",
            "Terminal" or "iTerm2" => "macOS Terminal",
            "Microsoft Teams" => "Microsoft Teams",
            "Pages" => "Apple Pages",
            "Numbers" => "Apple Numbers",
            "Keynote" => "Apple Keynote",
            _ => name
        };
    }

    public void FlushCurrentActivity()
    {
        var now = DateTime.UtcNow;
        var duration = (int)(now - _lastFocusChange).TotalSeconds;
        _lastFocusChange = now;

        if (duration > 0 && !string.IsNullOrWhiteSpace(_currentProcessName))
        {
            ActivityLogged?.Invoke(
                _currentProcessName,
                _currentWindowTitle,
                duration,
                _isIdle,
                _currentTaskId,
                _currentTaskCode,
                now);
        }
    }

    public void Dispose()
    {
        Stop();
        _pollTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
