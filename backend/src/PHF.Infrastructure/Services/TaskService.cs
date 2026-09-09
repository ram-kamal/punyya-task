using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class TaskService : ITaskService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public TaskService(ApplicationDbContext dbContext, IRealtimeNotifier realtimeNotifier)
    {
        _dbContext = dbContext;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<List<TaskResponseDto>> GetTasksAsync(
        string? status = null, 
        Guid? assignedTo = null, 
        string? stationId = null,
        bool includeUnassigned = false,
        string? priority = null, 
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Tasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.Attachments)
            .Include(t => t.ActivityLogs)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (assignedTo.HasValue)
        {
            if (includeUnassigned)
            {
                if (!string.IsNullOrWhiteSpace(stationId))
                {
                    query = query.Where(t => t.AssignedTo == assignedTo.Value || t.AssignedTo == null || (t.AssignedToUser != null && t.AssignedToUser.StationId == stationId));
                }
                else
                {
                    query = query.Where(t => t.AssignedTo == assignedTo.Value || t.AssignedTo == null);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(stationId))
                {
                    query = query.Where(t => t.AssignedTo == assignedTo.Value || (t.AssignedToUser != null && t.AssignedToUser.StationId == stationId));
                }
                else
                {
                    query = query.Where(t => t.AssignedTo == assignedTo.Value);
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(stationId))
        {
            if (includeUnassigned)
            {
                query = query.Where(t => (t.AssignedToUser != null && t.AssignedToUser.StationId == stationId) || t.AssignedTo == null);
            }
            else
            {
                query = query.Where(t => t.AssignedToUser != null && t.AssignedToUser.StationId == stationId);
            }
        }
        else if (includeUnassigned)
        {
            query = query.Where(t => t.AssignedTo == null);
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(t => t.Priority == priority);
        }

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tasks.Select(MapToDto).ToList();
    }

    public async Task<TaskResponseDto?> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Include(t => t.Attachments)
            .Include(t => t.ActivityLogs)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return task != null ? MapToDto(task) : null;
    }

    public async Task<TaskResponseDto> CreateTaskAsync(CreateTaskRequest request, Guid assignedBy, CancellationToken cancellationToken = default)
    {
        // Generate next task code (e.g. TASK-111)
        var maxTaskCodeNumber = 100;
        var existingCodes = await _dbContext.Tasks
            .Select(t => t.TaskCode)
            .ToListAsync(cancellationToken);

        foreach (var code in existingCodes)
        {
            if (code.StartsWith("TASK-", StringComparison.OrdinalIgnoreCase) && 
                int.TryParse(code[5..], out var num) && num > maxTaskCodeNumber)
            {
                maxTaskCodeNumber = num;
            }
        }

        var nextTaskCode = $"TASK-{maxTaskCodeNumber + 1}";

        var task = new TaskItem
        {
            TaskCode = nextTaskCode,
            Title = request.Title.Trim(),
            Description = request.Description,
            AssignedTo = request.AssignedTo,
            AssignedBy = assignedBy,
            Status = TaskItemStatus.ASSIGNED.ToString(),
            Priority = !string.IsNullOrWhiteSpace(request.Priority) ? request.Priority : TaskPriority.MEDIUM.ToString(),
            DeliverableRequired = request.DeliverableRequired,
            DueDate = request.DueDate,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Tasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Reload with users
        var createdTask = await GetTaskByIdAsync(task.Id, cancellationToken);

        if (createdTask != null && task.AssignedTo.HasValue)
        {
            await _realtimeNotifier.NotifyTaskAssignedAsync(task.AssignedTo.Value, createdTask);
            await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
            {
                StationId = createdTask.AssignedToStation ?? "ALL",
                UserName = createdTask.AssignedToName,
                EventType = "TASK_ASSIGNED",
                Details = $"Task dispatched: {createdTask.TaskCode} - {createdTask.Title}",
                TaskCode = createdTask.TaskCode
            });
        }

        return createdTask!;
    }

    public async Task<TaskResponseDto?> UpdateTaskStatusAsync(
        Guid id, 
        UpdateTaskStatusRequest request, 
        Guid updatedBy, 
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task == null) return null;

        var targetStatus = request.Status.ToUpperInvariant();

        // Deliverable Gate Validation (punnya-combined-spec.pptx)
        if (targetStatus == TaskItemStatus.COMPLETED.ToString() && task.DeliverableRequired)
        {
            var hasDeliverableUrl = !string.IsNullOrWhiteSpace(request.DeliverableUrl) || !string.IsNullOrWhiteSpace(task.DeliverableUrl);
            var hasAttachment = task.Attachments.Any();

            if (!hasDeliverableUrl && !hasAttachment)
            {
                throw new InvalidOperationException("Deliverable proof of work (file or link) is required before marking this task as Completed.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DeliverableUrl))
        {
            task.DeliverableUrl = request.DeliverableUrl;
        }

        if (!string.IsNullOrWhiteSpace(request.DeliverableFileName))
        {
            task.DeliverableFileName = request.DeliverableFileName;
        }

        if (targetStatus == TaskItemStatus.IN_PROGRESS.ToString() && !task.StartedAt.HasValue)
        {
            task.StartedAt = DateTime.UtcNow;
        }
        else if (targetStatus == TaskItemStatus.COMPLETED.ToString())
        {
            task.CompletedAt = DateTime.UtcNow;
        }

        task.Status = targetStatus;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyTaskStatusChangedAsync(task.Id, targetStatus, task.DeliverableUrl);

        var updated = await GetTaskByIdAsync(task.Id, cancellationToken);
        if (updated != null)
        {
            await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
            {
                StationId = updated.AssignedToStation ?? "STATION",
                UserName = updated.AssignedToName,
                EventType = "TASK_STATUS_CHANGED",
                Details = $"{updated.TaskCode} status updated to {targetStatus}",
                TaskCode = updated.TaskCode
            });
        }

        return updated;
    }

    public async Task<TaskAttachmentDto> AttachDeliverableAsync(
        AttachDeliverableRequest request, 
        Guid uploadedBy, 
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks.FindAsync(new object[] { request.TaskId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {request.TaskId} not found.");

        var attachment = new TaskAttachment
        {
            TaskId = request.TaskId,
            UploadedBy = uploadedBy,
            FileName = request.FileName,
            StoragePath = request.DeliverableUrl,
            FileSizeBytes = request.FileSizeBytes,
            MimeType = request.MimeType,
            UploadedAt = DateTime.UtcNow
        };

        _dbContext.TaskAttachments.Add(attachment);

        // Update task deliverable summary
        task.DeliverableUrl = request.DeliverableUrl;
        task.DeliverableFileName = request.FileName;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new TaskAttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            StoragePath = attachment.StoragePath,
            FileSizeBytes = attachment.FileSizeBytes,
            MimeType = attachment.MimeType,
            UploadedAt = attachment.UploadedAt
        };

        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = "STATION",
            EventType = "DELIVERABLE_ATTACHED",
            Details = $"Deliverable '{attachment.FileName}' attached to {task.TaskCode}",
            TaskCode = task.TaskCode
        });

        return result;
    }

    public async Task<bool> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks.FindAsync(new object[] { id }, cancellationToken);
        if (task == null) return false;

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static TaskResponseDto MapToDto(TaskItem t)
    {
        return new TaskResponseDto
        {
            Id = t.Id,
            TaskCode = t.TaskCode,
            Title = t.Title,
            Description = t.Description,
            AssignedTo = t.AssignedTo,
            AssignedToName = t.AssignedToUser?.FullName,
            AssignedToStation = t.AssignedToUser?.StationId,
            AssignedBy = t.AssignedBy,
            AssignedByName = t.AssignedByUser?.FullName,
            Status = t.Status,
            Priority = t.Priority,
            DeliverableRequired = t.DeliverableRequired,
            DeliverableUrl = t.DeliverableUrl,
            DeliverableFileName = t.DeliverableFileName,
            DueDate = t.DueDate,
            StartedAt = t.StartedAt,
            CompletedAt = t.CompletedAt,
            CreatedAt = t.CreatedAt,
            TotalTrackedSeconds = t.ActivityLogs?.Sum(l => l.DurationSeconds) ?? 0,
            Attachments = t.Attachments.Select(a => new TaskAttachmentDto
            {
                Id = a.Id,
                FileName = a.FileName,
                StoragePath = a.StoragePath,
                FileSizeBytes = a.FileSizeBytes,
                MimeType = a.MimeType,
                UploadedAt = a.UploadedAt
            }).ToList()
        };
    }
}
