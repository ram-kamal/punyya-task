using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PHF.Core.DTOs;

namespace PHF.Client.Core.Http;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private string? _authToken;
    private readonly JsonSerializerOptions _jsonOptions;

    public string BaseUrl { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_authToken);
    public string? AuthToken => _authToken;

    public ApiClient(string baseUrl = "http://localhost:5020")
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public void SetBaseUrl(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _httpClient.BaseAddress = new Uri(BaseUrl);
    }

    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password, string? stationId = null, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            StationId = stationId
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode) return null;

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, ct);
        if (auth != null && !string.IsNullOrWhiteSpace(auth.Token))
        {
            SetAuthToken(auth.Token);
        }

        return auth;
    }

    public async Task<List<TaskResponseDto>> GetAssignedTasksAsync(Guid? assignedTo = null, CancellationToken ct = default)
    {
        var url = "/api/v1/tasks";
        if (assignedTo.HasValue)
        {
            url += $"?assignedTo={assignedTo.Value}";
        }

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return new List<TaskResponseDto>();

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskResponseDto>>(_jsonOptions, ct);
        return tasks ?? new List<TaskResponseDto>();
    }

    public async Task<TaskResponseDto?> UpdateTaskStatusAsync(
        Guid taskId, 
        string status, 
        string? deliverableUrl = null, 
        string? deliverableFileName = null, 
        CancellationToken ct = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"/api/v1/tasks/{taskId}/status", new UpdateTaskStatusRequest
        {
            Status = status,
            DeliverableUrl = deliverableUrl,
            DeliverableFileName = deliverableFileName
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to update task: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<TaskResponseDto>(_jsonOptions, ct);
    }

    public async Task<TaskAttachmentDto?> UploadDeliverableAsync(Guid taskId, string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Deliverable file not found", filePath);
        }

        using var content = new MultipartFormDataContent();
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var streamContent = new StreamContent(fileStream);

        var fileName = Path.GetFileName(filePath);
        content.Add(streamContent, "file", fileName);

        var response = await _httpClient.PostAsync($"/api/v1/tasks/{taskId}/attachments", content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Upload failed: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<TaskAttachmentDto>(_jsonOptions, ct);
    }

    public async Task<ShiftResponseDto?> ClockInAsync(string stationId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/shifts/clock-in", new ShiftClockInRequest
        {
            StationId = stationId
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ShiftResponseDto>(_jsonOptions, ct);
    }

    public async Task<ShiftResponseDto?> ClockOutAsync(string stationId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/shifts/clock-out", new ShiftClockOutRequest
        {
            StationId = stationId
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ShiftResponseDto>(_jsonOptions, ct);
    }

    public async Task<ShiftResponseDto?> GetCurrentShiftAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/shifts/current", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ShiftResponseDto>(_jsonOptions, ct);
    }

    public async Task<bool> SendTelemetryBatchAsync(TelemetryBatchRequest batch, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/telemetry/batch", batch, _jsonOptions, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendHeartbeatAsync(WorkstationHeartbeatDto heartbeat, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/v1/telemetry/heartbeat", heartbeat, _jsonOptions, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<LeaveResponseDto?> ApplyLeaveAsync(CreateLeaveRequestDto request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/leaves", request, _jsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to submit leave: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<LeaveResponseDto>(_jsonOptions, ct);
    }

    public async Task<List<LeaveResponseDto>> GetMyLeavesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/leaves/my", ct);
        if (!response.IsSuccessStatusCode) return new List<LeaveResponseDto>();

        var leaves = await response.Content.ReadFromJsonAsync<List<LeaveResponseDto>>(_jsonOptions, ct);
        return leaves ?? new List<LeaveResponseDto>();
    }
}
