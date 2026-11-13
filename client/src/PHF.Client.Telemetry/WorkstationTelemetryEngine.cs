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
    public bool IsIdle => !IsDevelopmentEnvironment(CurrentProcessName, CurrentWindowTitle) && _idleTracker.IsIdle;
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
            var isDev = IsDevelopmentEnvironment(e.ProcessName, e.WindowTitle);
            var isIdle = !isDev && _idleTracker.IsIdle;
            ActivityLogged?.Invoke(
                e.ProcessName,
                e.WindowTitle,
                e.DurationPreviousSeconds,
                isIdle,
                _currentTaskId,
                _currentTaskCode,
                e.Timestamp);
        }

        FocusChanged?.Invoke(_windowTracker.CurrentProcessName, _windowTracker.CurrentWindowTitle);
    }

    private void OnIdleStatusChanged(object? sender, IdleStatusChangedEventArgs e)
    {
        var currentProc = _windowTracker.CurrentProcessName;
        var currentTitle = _windowTracker.CurrentWindowTitle;
        var isDev = IsDevelopmentEnvironment(currentProc, currentTitle);

        IdleStateChanged?.Invoke(isDev ? false : e.IsIdle, e.IdleSeconds);

        if (e.IsIdle)
        {
            if (isDev)
            {
                // In an IDE / Development environment (or while mobile/background dev tasks are running):
                // Reading code, awaiting agent reasoning, building or compiling is active work.
                // Do not convert the active IDE window segment into System Idle.
                return;
            }

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
            if (isDev)
            {
                return;
            }

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
            var isDev = IsDevelopmentEnvironment(process, title);
            var isIdle = !isDev && _idleTracker.IsIdle;
            ActivityLogged?.Invoke(
                process,
                title,
                duration,
                isIdle,
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

    #region Development Environment & Mobile Background Task Classification

    private static readonly HashSet<string> KnownDevelopmentProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        // Antigravity & AI IDEs
        "antigravity",
        "antigravity ide",
        "antigravity-x64",
        "cursor",
        "windsurf",

        // Microsoft / .NET
        "code",
        "code - insiders",
        "visual studio code",
        "devenv",
        "visual studio",

        // Mobile & Android
        "studio64",
        "studio",
        "android studio",
        "emulator",
        "qemu-system-x86_64",
        "adb",

        // JetBrains Suite
        "idea64",
        "idea",
        "intellij idea",
        "pycharm64",
        "pycharm",
        "webstorm64",
        "webstorm",
        "rider64",
        "rider",
        "clion64",
        "clion",
        "goland64",
        "goland",
        "phpstorm64",
        "phpstorm",
        "rubymine64",
        "rubymine",
        "datagrip64",
        "datagrip",

        // Other Editors & Tools
        "sublime_text",
        "sublime text",
        "eclipse",
        "netbeans",
        "notepad++",

        // Terminals & Consoles
        "terminal",
        "windowsterminal",
        "powershell",
        "pwsh",
        "cmd",
        "git-bash",
        "bash",
        "wsl",

        // Compilers, Runtimes & Mobile/Build Daemons
        "dotnet",
        "node",
        "python",
        "python3",
        "gradle",
        "gradlew",
        "java",
        "javaw"
    };

    public static bool IsDevelopmentEnvironment(string? processName, string? windowTitle = null)
    {
        if (string.IsNullOrWhiteSpace(processName) && string.IsNullOrWhiteSpace(windowTitle))
            return false;

        var proc = (processName ?? "").Trim();
        if (proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            proc = proc[..^4];

        if (KnownDevelopmentProcesses.Contains(proc))
            return true;

        var lowerProc = proc.ToLowerInvariant();
        if (lowerProc.Contains("antigravity") ||
            lowerProc.Contains("studio") ||
            lowerProc.Contains("devenv") ||
            lowerProc.Contains("intellij") ||
            lowerProc.Contains("pycharm") ||
            lowerProc.Contains("webstorm") ||
            lowerProc.Contains("rider") ||
            lowerProc.Contains("cursor") ||
            lowerProc.Contains("windsurf") ||
            lowerProc.Contains("emulator"))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            var title = windowTitle.Trim();
            if (title.Contains("Antigravity", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Visual Studio", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Android Studio", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("IntelliJ", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("PyCharm", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("WebStorm", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Rider", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("CLion", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Cursor", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Windsurf", StringComparison.OrdinalIgnoreCase) ||
                title.Contains(".csproj", StringComparison.OrdinalIgnoreCase) ||
                title.Contains(".sln", StringComparison.OrdinalIgnoreCase) ||
                title.Contains(".gradle", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
