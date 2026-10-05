using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;

namespace PHF.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;
    private readonly IStorageService _storageService;

    public TasksController(ITaskService taskService, IStorageService storageService)
    {
        _taskService = taskService;
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromQuery] string? status = null,
        [FromQuery] Guid? assignedTo = null,
        [FromQuery] string? stationId = null,
        [FromQuery] bool includeUnassigned = false,
        [FromQuery] string? priority = null,
        CancellationToken cancellationToken = default)
    {
        var isManagerOrAdmin = User.IsInRole("ADMIN") || User.IsInRole("MANAGER") || User.IsInRole("MONITOR");
        var currentUserId = GetCurrentUserId();

        if (!isManagerOrAdmin)
        {
            // Non-managers can only query tasks assigned to themselves or unassigned tasks
            if (!assignedTo.HasValue || assignedTo.Value != currentUserId)
            {
                assignedTo = currentUserId;
            }
        }

        var tasks = await _taskService.GetTasksAsync(status, assignedTo, stationId, includeUnassigned, priority, cancellationToken);
        return Ok(tasks);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTaskById(Guid id, CancellationToken cancellationToken)
    {
        var task = await _taskService.GetTaskByIdAsync(id, cancellationToken);
        if (task == null) return NotFound(new { message = $"Task {id} not found" });

        return Ok(task);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var task = await _taskService.CreateTaskAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetTaskById), new { id = task.Id }, task);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTask(Guid id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var updated = await _taskService.UpdateTaskAsync(id, request, userId, cancellationToken);
            if (updated == null) return NotFound(new { message = $"Task {id} not found" });

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, 
        [FromBody] UpdateTaskStatusRequest request, 
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var updated = await _taskService.UpdateTaskStatusAsync(id, request, userId, cancellationToken);
            if (updated == null) return NotFound(new { message = $"Task {id} not found" });

            return Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            // Deliverable gate validation error
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{id:guid}/attachments")]
    public async Task<IActionResult> UploadAttachment(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file provided." });
        }

        try
        {
            var userId = GetCurrentUserId();
            using var stream = file.OpenReadStream();
            var storagePath = await _storageService.SaveFileAsync(file.FileName, stream, file.ContentType, cancellationToken);

            var attachment = await _taskService.AttachDeliverableAsync(new AttachDeliverableRequest
            {
                TaskId = id,
                DeliverableUrl = storagePath,
                FileName = file.FileName,
                FileSizeBytes = file.Length,
                MimeType = file.ContentType
            }, userId, cancellationToken);

            return Ok(attachment);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{id:guid}/attachments/presign")]
    public IActionResult PresignAttachmentUrl(Guid id, [FromBody] PresignRequest request)
    {
        // For S3 compatibility or local storage direct upload endpoint
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var uploadUrl = $"{baseUrl}/api/v1/tasks/{id}/attachments";

        return Ok(new
        {
            TaskId = id,
            UploadUrl = uploadUrl,
            Key = $"{id}/{Guid.NewGuid():N}_{request.FileName}",
            Method = "POST"
        });
    }

    [Authorize]
    [HttpPost("{id:guid}/attachments/complete")]
    public async Task<IActionResult> CompleteAttachment(
        Guid id, 
        [FromBody] CompleteAttachmentRequest request, 
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var attachment = await _taskService.AttachDeliverableAsync(new AttachDeliverableRequest
        {
            TaskId = id,
            DeliverableUrl = request.StoragePath ?? request.DeliverableUrl ?? "",
            FileName = request.FileName,
            FileSizeBytes = request.FileSizeBytes,
            MimeType = request.MimeType
        }, userId, cancellationToken);

        return Ok(attachment);
    }

    [Authorize]
    [HttpPost("{id:guid}/attachments/link")]
    public async Task<IActionResult> AttachDeliverableLink(
        Guid id,
        [FromBody] AttachDeliverableLinkRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest(new { message = "Deliverable URL or link is required." });
        }

        var trimmedUrl = request.Url.Trim();
        if (!trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmedUrl = "https://" + trimmedUrl;
        }

        var userId = GetCurrentUserId();
        var fileName = !string.IsNullOrWhiteSpace(request.FileName) 
            ? request.FileName.Trim() 
            : "External Proof Link";

        var attachment = await _taskService.AttachDeliverableAsync(new AttachDeliverableRequest
        {
            TaskId = id,
            DeliverableUrl = trimmedUrl,
            FileName = fileName,
            FileSizeBytes = 0,
            MimeType = "text/uri-list"
        }, userId, cancellationToken);

        return Ok(attachment);
    }

    [HttpGet("{id:guid}/deliverable")]
    public async Task<IActionResult> DownloadDeliverable(Guid id, CancellationToken cancellationToken)
    {
        var task = await _taskService.GetTaskByIdAsync(id, cancellationToken);
        if (task == null || string.IsNullOrWhiteSpace(task.DeliverableUrl))
        {
            return NotFound(new { message = "No deliverable found for this task." });
        }

        // External Link Deliverable: Redirect immediately to destination URL
        if (task.DeliverableUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            task.DeliverableUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(task.DeliverableUrl);
        }

        var fileResult = await _storageService.GetFileAsync(task.DeliverableUrl, cancellationToken);
        if (fileResult == null)
        {
            return NotFound(new { message = "Deliverable file not found on storage." });
        }

        return File(fileResult.Value.Stream, fileResult.Value.ContentType, fileResult.Value.FileName);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken cancellationToken)
    {
        var success = await _taskService.DeleteTaskAsync(id, cancellationToken);
        if (!success) return NotFound();
        return NoContent();
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDeleteTasks([FromBody] BulkDeleteTasksRequest request, CancellationToken cancellationToken)
    {
        var result = await _taskService.BulkDeleteTasksAsync(request.TaskIds, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("delete-all")]
    public async Task<IActionResult> DeleteAllTasks(CancellationToken cancellationToken)
    {
        var adminId = GetCurrentUserId();
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? "Admin";
        var result = await _taskService.DeleteAllTasksAsync(adminId, adminName, cancellationToken);
        return Ok(result);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpGet("backups")]
    public async Task<IActionResult> GetTaskBackups(CancellationToken cancellationToken)
    {
        var backups = await _taskService.GetTaskBackupsAsync(cancellationToken);
        return Ok(backups);
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPost("backups/{id:guid}/restore")]
    public async Task<IActionResult> RestoreTaskBackup(Guid id, CancellationToken cancellationToken)
    {
        var adminId = GetCurrentUserId();
        var result = await _taskService.RestoreTaskBackupAsync(id, adminId, cancellationToken);
        if (result == null) return NotFound(new { message = "Backup snapshot not found." });
        return Ok(result);
    }

    [Authorize(Roles = "ADMIN,MANAGER")]
    [HttpPost("backups/{id:guid}/dismiss-reminder")]
    public async Task<IActionResult> DismissBackupReminder(Guid id, [FromBody] DismissReminderRequest request, CancellationToken cancellationToken)
    {
        var success = await _taskService.DismissBackupReminderAsync(id, request.MilestoneDay, cancellationToken);
        if (!success) return NotFound();
        return Ok(new { success = true });
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(claim) && Guid.TryParse(claim, out var guid))
        {
            return guid;
        }

        throw new UnauthorizedAccessException("Authentication token does not contain a valid user identity claim.");
    }
}

public record PresignRequest(string FileName, string? ContentType);
public record CompleteAttachmentRequest(string FileName, string? StoragePath, string? DeliverableUrl, long FileSizeBytes, string? MimeType);
