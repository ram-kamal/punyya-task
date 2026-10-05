using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PHF.Core.DTOs;

namespace PHF.Client.Core.Http;

public class AccountSuspendedException : Exception
{
    public AccountSuspendedException(string message) : base(message) { }
}

public class ApiClient
{
    private HttpClient _httpClient;
    private string? _authToken;
    private readonly JsonSerializerOptions _jsonOptions;

    public const string DefaultBaseUrl = "https://punnya-api.onrender.com";
    public string BaseUrl { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_authToken);
    public string? AuthToken => _authToken;

    public static string ResolveBaseUrl(string? url = null)
    {
        if (!string.IsNullOrWhiteSpace(url)) return url.TrimEnd('/');
        var envUrl = Environment.GetEnvironmentVariable("PHF_API_URL");
        return (!string.IsNullOrWhiteSpace(envUrl) ? envUrl : DefaultBaseUrl).TrimEnd('/');
    }

    public ApiClient(string? baseUrl = null)
    {
        BaseUrl = ResolveBaseUrl(baseUrl);
        _httpClient = CreateHttpClient(BaseUrl, _authToken);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private static HttpClient CreateHttpClient(string baseUrl, string? token)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(15)
        };
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    public void SetBaseUrl(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _httpClient = CreateHttpClient(BaseUrl, _authToken);
    }

    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void Logout()
    {
        _authToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password, string? stationId = null, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Email = email,
            Password = password,
            StationId = stationId
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                try
                {
                    var err = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(_jsonOptions, ct);
                    if (err != null && err.TryGetValue("message", out var msg) && !string.IsNullOrWhiteSpace(msg))
                    {
                        throw new AccountSuspendedException(msg);
                    }
                }
                catch (AccountSuspendedException) { throw; }
                catch { }

                throw new AccountSuspendedException("This account is suspended or inactive. Please contact your system administrator.");
            }

            return null;
        }

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(_jsonOptions, ct);
        if (auth != null && !string.IsNullOrWhiteSpace(auth.Token))
        {
            SetAuthToken(auth.Token);
        }

        return auth;
    }

    public async Task<List<TaskResponseDto>> GetAssignedTasksAsync(
        Guid? assignedTo = null, 
        string? stationId = null, 
        bool includeUnassigned = true, 
        CancellationToken ct = default)
    {
        var queryParams = new List<string>();
        if (assignedTo.HasValue)
        {
            queryParams.Add($"assignedTo={assignedTo.Value}");
        }
        if (!string.IsNullOrWhiteSpace(stationId))
        {
            queryParams.Add($"stationId={Uri.EscapeDataString(stationId)}");
        }
        if (includeUnassigned)
        {
            queryParams.Add("includeUnassigned=true");
        }

        var url = "/api/v1/tasks" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return new List<TaskResponseDto>();

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskResponseDto>>(_jsonOptions, ct);
        return tasks ?? new List<TaskResponseDto>();
    }

    public async Task<List<TaskResponseDto>> GetAllTasksAsync(string? status = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(status)
            ? "/api/v1/tasks"
            : $"/api/v1/tasks?status={Uri.EscapeDataString(status)}";

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
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var mime = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".zip" => "application/zip",
            ".rar" => "application/x-rar-compressed",
            ".7z" => "application/x-7z-compressed",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
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

    public async Task<TaskAttachmentDto?> AttachDeliverableLinkAsync(Guid taskId, string url, string? fileName = null, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/tasks/{taskId}/attachments/link", new AttachDeliverableLinkRequest
        {
            Url = url,
            FileName = fileName
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Attaching link failed: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<TaskAttachmentDto>(_jsonOptions, ct);
    }

    public async Task<List<WorkstationStatusDto>> GetWorkstationsStatusAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/telemetry/stations", ct);
        if (!response.IsSuccessStatusCode) return new List<WorkstationStatusDto>();

        var list = await response.Content.ReadFromJsonAsync<List<WorkstationStatusDto>>(_jsonOptions, ct);
        return list ?? new List<WorkstationStatusDto>();
    }

    public async Task<List<LiveWorkstationEventDto>> GetRecentEventsAsync(int limit = 100, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/v1/telemetry/events?limit={limit}", ct);
        if (!response.IsSuccessStatusCode) return new List<LiveWorkstationEventDto>();

        var list = await response.Content.ReadFromJsonAsync<List<LiveWorkstationEventDto>>(_jsonOptions, ct);
        return list ?? new List<LiveWorkstationEventDto>();
    }

    public async Task<TaskResponseDto?> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/tasks", request, _jsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to dispatch task: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<TaskResponseDto>(_jsonOptions, ct);
    }

    public async Task<TimesheetSummaryDto?> GetTimesheetSummaryAsync(string period = "today", DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default)
    {
        var query = $"?period={Uri.EscapeDataString(period)}";
        if (startDate.HasValue) query += $"&startDate={startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue) query += $"&endDate={endDate.Value:yyyy-MM-dd}";

        var response = await _httpClient.GetAsync($"/api/v1/timesheet/summary{query}", ct);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadFromJsonAsync<TimesheetSummaryDto>(_jsonOptions, ct);
    }

    public async Task<byte[]?> ExportTimesheetCsvAsync(DateTime? startDate = null, DateTime? endDate = null, string? period = "month", CancellationToken ct = default)
    {
        var query = $"?period={Uri.EscapeDataString(period ?? "today")}";
        if (startDate.HasValue) query += $"&startDate={startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue) query += $"&endDate={endDate.Value:yyyy-MM-dd}";

        var response = await _httpClient.GetAsync($"/api/v1/timesheet/export/csv{query}", ct);
        if (!response.IsSuccessStatusCode) return null;

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<List<LeaveResponseDto>> GetAllLeavesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/leaves", ct);
        if (!response.IsSuccessStatusCode) return new List<LeaveResponseDto>();

        var list = await response.Content.ReadFromJsonAsync<List<LeaveResponseDto>>(_jsonOptions, ct);
        return list ?? new List<LeaveResponseDto>();
    }

    public async Task<LeaveResponseDto?> ReviewLeaveAsync(Guid leaveId, string status, string? comments = null, CancellationToken ct = default)
    {
        var action = status.Equals("REJECT", StringComparison.OrdinalIgnoreCase) || status.Equals("REJECTED", StringComparison.OrdinalIgnoreCase)
            ? "reject"
            : "approve";
        var response = await _httpClient.PatchAsJsonAsync($"/api/v1/leaves/{leaveId}/{action}", new ReviewLeaveRequestDto
        {
            Comments = comments
        }, _jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to review leave: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<LeaveResponseDto>(_jsonOptions, ct);
    }

    public async Task<List<StaffMemberDto>> GetAllStaffAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("/api/v1/staff", ct);
        if (!response.IsSuccessStatusCode) return new List<StaffMemberDto>();

        var list = await response.Content.ReadFromJsonAsync<List<StaffMemberDto>>(_jsonOptions, ct);
        return list ?? new List<StaffMemberDto>();
    }

    public async Task<StaffMemberDto?> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/v1/staff", request, _jsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to create staff member: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<StaffMemberDto>(_jsonOptions, ct);
    }

    public async Task<StaffMemberDto?> UpdateStaffAsync(Guid id, UpdateStaffRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/staff/{id}", request, _jsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to update staff member: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<StaffMemberDto>(_jsonOptions, ct);
    }

    public async Task<bool> ToggleStaffStatusAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.PatchAsync($"/api/v1/staff/{id}/toggle-status", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ResetStaffPasswordAsync(Guid id, string newPassword, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"/api/v1/staff/{id}/reset-password", new ResetPasswordRequest
        {
            NewPassword = newPassword
        }, _jsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateStaffWorkModeAsync(Guid id, string workMode, CancellationToken ct = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"/api/v1/staff/{id}/work-mode", new UpdateWorkModeRequest
        {
            WorkMode = workMode
        }, _jsonOptions, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteStaffAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/staff/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<TaskResponseDto?> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"/api/v1/tasks/{taskId}", request, _jsonOptions, ct);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Failed to update task: {response.StatusCode} - {err}");
        }

        return await response.Content.ReadFromJsonAsync<TaskResponseDto>(_jsonOptions, ct);
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/tasks/{taskId}", ct);
        return response.IsSuccessStatusCode;
    }
}
