namespace PHF.Client.Core.Telemetry;

/// <summary>
/// Cross-platform abstraction for workstation telemetry collection (Windows and macOS).
/// </summary>
public interface ITelemetryEngine : IDisposable
{
    event Action<string, string, int, bool, Guid?, string?, DateTime>? ActivityLogged;
    event Action<string, string, DateTime>? SessionLogged;
    event Action<bool, int>? IdleStateChanged;
    event Action<string, string>? FocusChanged;

    string CurrentProcessName { get; }
    string CurrentWindowTitle { get; }
    bool IsIdle { get; }
    int IdleSeconds { get; }
    Guid? CurrentTaskId { get; }
    string? CurrentTaskCode { get; }

    void Start();
    void Stop();
    void SetCurrentTask(Guid? taskId, string? taskCode);
    void FlushCurrentActivity();
}
