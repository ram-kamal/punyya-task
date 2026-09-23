using Microsoft.AspNetCore.SignalR.Client;
using PHF.Core.DTOs;

namespace PHF.Client.Core.Realtime;

public class RealtimeClient : IAsyncDisposable, IDisposable
{
    private HubConnection? _hubConnection;
    private string _hubUrl;
    private Func<string?>? _tokenProvider;

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
        if (_hubConnection != null)
        {
            await StopAsync();
        }

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_tokenProvider?.Invoke());
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) })
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
                if (!string.IsNullOrWhiteSpace(stationId))
                {
                    await _hubConnection.InvokeAsync("JoinStation", stationId);
                }
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    await _hubConnection.InvokeAsync("JoinUserGroup", userId);
                }
                // Automatically notify client to reload assignments upon reconnection
                OnTaskAssigned?.Invoke(null!);
            }
            catch { }
        };

        _hubConnection.Closed += _ =>
        {
            OnConnectionStateChanged?.Invoke(false);
            return Task.CompletedTask;
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
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            OnConnectionStateChanged?.Invoke(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        try { StopAsync().GetAwaiter().GetResult(); } catch { }
        GC.SuppressFinalize(this);
    }
}

public record TaskStatusUpdatePayload(Guid TaskId, string NewStatus, string? DeliverableUrl);
