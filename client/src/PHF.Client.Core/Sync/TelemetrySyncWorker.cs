using System.Diagnostics;
using PHF.Client.Core.Http;
using PHF.Client.Core.Storage;
using PHF.Client.Telemetry;
using PHF.Core.DTOs;
using PHF.Core.Enums;

namespace PHF.Client.Core.Sync;

public class TelemetrySyncWorker : IDisposable
{
    private readonly TamperProofDatabase _database;
    private readonly ApiClient _apiClient;
    private readonly WorkstationTelemetryEngine _engine;
    private readonly Timer _syncTimer;
    private readonly Timer _heartbeatTimer;

    private string? _userId;
    private string? _stationId;
    private string? _userFullName;
    private bool _isSyncing = false;

    public event Action<int, DateTime>? OnBatchSynced;
    public event Action<bool>? OnSyncStatusChanged;

    public TelemetrySyncWorker(
        TamperProofDatabase database, 
        ApiClient apiClient, 
        WorkstationTelemetryEngine engine,
        int syncIntervalSeconds = 30)
    {
        _database = database;
        _apiClient = apiClient;
        _engine = engine;

        // Hook engine events to enqueue into SQLite buffer
        _engine.ActivityLogged += OnEngineActivityLogged;
        _engine.SessionLogged += OnEngineSessionLogged;

        // Timers
        _syncTimer = new Timer(async _ => await SyncPendingBatchAsync(), null, Timeout.Infinite, Timeout.Infinite);
        _heartbeatTimer = new Timer(async _ => await SendHeartbeatAsync(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void ConfigureUser(string userId, string userFullName, string stationId)
    {
        _userId = userId;
        _userFullName = userFullName;
        _stationId = stationId;
    }

    public void Start()
    {
        _syncTimer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        _heartbeatTimer.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(15));
    }

    public void Stop()
    {
        _syncTimer.Change(Timeout.Infinite, Timeout.Infinite);
        _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void OnEngineActivityLogged(
        string processName, 
        string windowTitle, 
        int duration, 
        bool isIdle, 
        Guid? taskId, 
        string? taskCode, 
        DateTime loggedAt)
    {
        _database.EnqueueActivityLog(
            _userId,
            taskId,
            taskCode,
            processName,
            windowTitle,
            duration,
            isIdle,
            loggedAt);
    }

    private void OnEngineSessionLogged(string eventType, string machineName, DateTime timestamp)
    {
        _database.EnqueueSessionEvent(_userId, machineName, eventType, timestamp);
    }

    public async Task SyncPendingBatchAsync()
    {
        if (_isSyncing || !_apiClient.IsAuthenticated) return;
        _isSyncing = true;

        try
        {
            // Flush current running window segment so real-time activity is captured
            _engine.FlushCurrentActivity();

            var pendingLogs = _database.GetPendingActivityLogs(50);
            var pendingSessions = _database.GetPendingSessions(50);

            if (!pendingLogs.Any() && !pendingSessions.Any())
            {
                _isSyncing = false;
                return;
            }

            // Exclude tampered records if any (alert in debug)
            var validLogs = pendingLogs.Where(l => !l.IsTampered).ToList();
            var validSessions = pendingSessions.Where(s => !s.IsTampered).ToList();

            var batch = new TelemetryBatchRequest
            {
                StationId = _stationId,
                UserId = Guid.TryParse(_userId, out var guid) ? guid : null,
                SessionEvents = validSessions.Select(s => new UserSessionDto
                {
                    MachineName = s.MachineName,
                    EventType = s.EventType,
                    EventTimestamp = s.EventTimestamp
                }).ToList(),
                ActivityLogs = validLogs.Select(l => new AppActivityLogDto
                {
                    TaskId = Guid.TryParse(l.TaskId, out var tid) ? tid : null,
                    TaskCode = l.TaskCode,
                    ProcessName = l.ProcessName,
                    WindowTitle = l.WindowTitle,
                    DurationSeconds = l.DurationSeconds,
                    IsIdle = l.IsIdle,
                    LoggedAt = l.LoggedAt
                }).ToList()
            };

            var success = await _apiClient.SendTelemetryBatchAsync(batch);
            if (success)
            {
                // Delete processed items from local SQLite buffer
                _database.DeleteProcessedActivityLogs(pendingLogs.Select(l => l.Id));
                _database.DeleteProcessedSessions(pendingSessions.Select(s => s.Id));

                var totalCount = pendingLogs.Count + pendingSessions.Count;
                OnBatchSynced?.Invoke(totalCount, DateTime.UtcNow);
                OnSyncStatusChanged?.Invoke(true);
            }
            else
            {
                OnSyncStatusChanged?.Invoke(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SyncWorker] Error syncing batch: {ex.Message}");
            OnSyncStatusChanged?.Invoke(false);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async Task SendHeartbeatAsync()
    {
        if (string.IsNullOrWhiteSpace(_stationId)) return;

        var state = _engine.IsIdle ? StationState.IDLE.ToString() : StationState.ACTIVE.ToString();

        var heartbeat = new WorkstationHeartbeatDto
        {
            StationId = _stationId,
            UserId = Guid.TryParse(_userId, out var uid) ? uid : null,
            UserFullName = _userFullName,
            CurrentTaskCode = _engine.CurrentTaskCode,
            ActiveProcess = _engine.CurrentProcessName,
            WindowTitle = _engine.CurrentWindowTitle,
            State = state
        };

        await _apiClient.SendHeartbeatAsync(heartbeat);
    }

    public void Dispose()
    {
        _syncTimer.Dispose();
        _heartbeatTimer.Dispose();

        _engine.ActivityLogged -= OnEngineActivityLogged;
        _engine.SessionLogged -= OnEngineSessionLogged;

        GC.SuppressFinalize(this);
    }
}
