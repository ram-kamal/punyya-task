using Microsoft.AspNetCore.SignalR.Client;
using PHF.Core.DTOs;

namespace PHF.Client.Core.Realtime;

public class InfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var count = retryContext.PreviousRetryCount;
        if (count == 0) return TimeSpan.Zero;
        if (count < 3) return TimeSpan.FromSeconds(2);
        if (count < 6) return TimeSpan.FromSeconds(5);
        if (count < 10) return TimeSpan.FromSeconds(10);
        return TimeSpan.FromSeconds(15);
    }
}

public class RealtimeClient : IAsyncDisposable, IDisposable
{
    private HubConnection? _hubConnection;
    private string _hubUrl;
    private Func<string?>? _tokenProvider;
    private bool _isDisposed = false;
    private string? _lastStationId;
    private string? _lastUserId;

    public event Action<TaskResponseDto>? OnTaskAssigned;
    public event Action<Guid, string, string?>? OnTaskStatusChanged;
    public event Action<LiveWorkstationEventDto>? OnWorkstationEventReceived;
    public event Action<LiveWorkstationEventDto>? OnLeaveUpdated;
    public event Action<bool>? OnConnectionStateChanged;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public const string DefaultBaseUrl = "https://punnya-api.onrender.com";

    public RealtimeClient(string? baseUrl = null, Func<string?>? tokenProvider = null)
    {
        var resolved = !string.IsNullOrWhiteSpace(baseUrl) 
            ? baseUrl.TrimEnd('/') 
            : (Environment.GetEnvironmentVariable("PHF_API_URL") ?? DefaultBaseUrl).TrimEnd('/');
        _hubUrl = $"{resolved}/hubs/workstation";
        _tokenProvider = tokenProvider;
    }

    public void SetBaseUrl(string baseUrl)
    {
        _hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/workstation";
    }

    public void SetTokenProvider(Func<string?> tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public async Task StartAsync(string? stationId = null, string? userId = null, CancellationToken ct = default)
    {
        _lastStationId = stationId ?? _lastStationId;
        _lastUserId = userId ?? _lastUserId;

        if (_hubConnection != null)
        {
            await StopAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_tokenProvider?.Invoke());
            })
            .WithAutomaticReconnect(new InfiniteRetryPolicy())
            .Build();

        _hubConnection.On<TaskResponseDto>("TaskAssigned", task =>
        {
            OnTaskAssigned?.Invoke(task);
        });

        _hubConnection.On<TaskResponseDto>("TaskUpdated", task =>
        {
            OnTaskAssigned?.Invoke(task);
        });

        _hubConnection.On<TaskStatusUpdatePayload>("TaskStatusChanged", update =>
        {
            if (update != null)
            {
                OnTaskStatusChanged?.Invoke(update.TaskId, update.NewStatus, update.DeliverableUrl);
            }
        });

        _hubConnection.On<LiveWorkstationEventDto>("WorkstationEventReceived", evt =>
        {
            OnWorkstationEventReceived?.Invoke(evt);
        });

        _hubConnection.On<LiveWorkstationEventDto>("LeaveUpdated", evt =>
        {
            OnLeaveUpdated?.Invoke(evt);
        });

        _hubConnection.Reconnecting += _ =>
        {
            OnConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async _ =>
        {
            OnConnectionStateChanged?.Invoke(true);
            try
            {
                if (!string.IsNullOrWhiteSpace(_lastStationId))
                {
                    await _hubConnection.InvokeAsync("JoinStation", _lastStationId);
                }
                if (!string.IsNullOrWhiteSpace(_lastUserId))
                {
                    await _hubConnection.InvokeAsync("JoinUserGroup", _lastUserId);
                }
                // Automatically notify client to reload assignments upon reconnection
                OnTaskAssigned?.Invoke(null!);
            }
            catch { }
        };

        _hubConnection.Closed += async (error) =>
        {
            OnConnectionStateChanged?.Invoke(false);
            // Persistent background auto-reconnect loop: keep retrying forever until reconnected or disposed
            while (!_isDisposed && (_hubConnection?.State == HubConnectionState.Disconnected))
            {
                try
                {
                    await Task.Delay(5000);
                    if (_isDisposed) break;
                    if (_hubConnection != null)
                    {
                        await _hubConnection.StartAsync();
                        OnConnectionStateChanged?.Invoke(true);
                        if (!string.IsNullOrWhiteSpace(_lastStationId))
                        {
                            await _hubConnection.InvokeAsync("JoinStation", _lastStationId);
                        }
                        if (!string.IsNullOrWhiteSpace(_lastUserId))
                        {
                            await _hubConnection.InvokeAsync("JoinUserGroup", _lastUserId);
                        }
                        OnTaskAssigned?.Invoke(null!);
                        break;
                    }
                }
                catch
                {
                    // Continue retrying indefinitely
                }
            }
        };

        try
        {
            await _hubConnection.StartAsync(ct);
            OnConnectionStateChanged?.Invoke(true);

            if (!string.IsNullOrWhiteSpace(stationId))
            {
                await _hubConnection.InvokeAsync("JoinStation", stationId, ct);
            }
            if (!string.IsNullOrWhiteSpace(userId))
            {
                await _hubConnection.InvokeAsync("JoinUserGroup", userId, ct);
            }
        }
        catch
        {
            OnConnectionStateChanged?.Invoke(false);
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _hubConnection.StopAsync(cts.Token);
            }
            catch { }

            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch { }

            _hubConnection = null;
            OnConnectionStateChanged?.Invoke(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposed = true;
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        _isDisposed = true;
        try
        {
            Task.Run(async () => await StopAsync()).Wait(TimeSpan.FromMilliseconds(500));
        }
        catch { }
        GC.SuppressFinalize(this);
    }
}

public record TaskStatusUpdatePayload(Guid TaskId, string NewStatus, string? DeliverableUrl);
