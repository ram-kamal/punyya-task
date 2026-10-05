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
            DueDate = request.DueDate.HasValue ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc) : null,
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

    public async Task<TaskResponseDto?> UpdateTaskAsync(
        Guid id,
        UpdateTaskRequest request,
        Guid updatedBy,
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (task == null) return null;

        var previousAssignedTo = task.AssignedTo;

        if (!string.IsNullOrWhiteSpace(request.Title))
            task.Title = request.Title.Trim();

        if (request.Description != null)
            task.Description = request.Description;

        if (request.AssignedTo.HasValue)
            task.AssignedTo = request.AssignedTo.Value;

        if (!string.IsNullOrWhiteSpace(request.Priority))
            task.Priority = request.Priority.ToUpperInvariant();

        if (request.DeliverableRequired.HasValue)
            task.DeliverableRequired = request.DeliverableRequired.Value;

        if (request.DueDate.HasValue)
            task.DueDate = DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var targetStatus = request.Status.ToUpperInvariant();
            if (targetStatus == TaskItemStatus.COMPLETED.ToString() && task.DeliverableRequired)
            {
                var hasDeliverableUrl = !string.IsNullOrWhiteSpace(task.DeliverableUrl);
                var hasAttachment = task.Attachments.Any();
                if (!hasDeliverableUrl && !hasAttachment)
                {
                    throw new InvalidOperationException("Deliverable proof of work (file or link) is required before marking this task as Completed.");
                }
            }

            if (targetStatus == TaskItemStatus.IN_PROGRESS.ToString() && !task.StartedAt.HasValue)
            {
                task.StartedAt = DateTime.UtcNow;
            }
            else if (targetStatus == TaskItemStatus.COMPLETED.ToString() && !task.CompletedAt.HasValue)
            {
                task.CompletedAt = DateTime.UtcNow;
            }

            task.Status = targetStatus;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await GetTaskByIdAsync(task.Id, cancellationToken);
        if (updated != null)
        {
            if (task.AssignedTo.HasValue && task.AssignedTo != previousAssignedTo)
            {
                await _realtimeNotifier.NotifyTaskAssignedAsync(task.AssignedTo.Value, updated);
            }

            await _realtimeNotifier.NotifyTaskStatusChangedAsync(task.Id, task.Status, task.DeliverableUrl);
            await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
            {
                StationId = updated.AssignedToStation ?? "ALL",
                UserName = updated.AssignedToName,
                EventType = "TASK_UPDATED",
                Details = $"Task updated/reassigned: {updated.TaskCode} - {updated.Title}",
                TaskCode = updated.TaskCode
            });
        }

        return updated;
    }

    public async Task<bool> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.Tasks
            .Include(t => t.Attachments)
            .Include(t => t.ActivityLogs)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task == null) return false;

        if (task.ActivityLogs != null && task.ActivityLogs.Any())
        {
            _dbContext.AppActivityLogs.RemoveRange(task.ActivityLogs);
        }

        if (task.Attachments != null && task.Attachments.Any())
        {
            _dbContext.TaskAttachments.RemoveRange(task.Attachments);
        }

        _dbContext.Tasks.Remove(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = "ALL",
            EventType = "TASK_DELETED",
            Details = $"Task removed: {task.TaskCode} - {task.Title}",
            TaskCode = task.TaskCode
        });

        return true;
    }

    private static readonly int[] ReminderMilestoneDays = { 5, 15, 20, 25, 28, 29, 30 };

    public async Task<BulkDeleteTasksResponse> BulkDeleteTasksAsync(List<Guid> taskIds, CancellationToken cancellationToken = default)
    {
        if (taskIds == null || taskIds.Count == 0)
        {
            return new BulkDeleteTasksResponse { DeletedCount = 0, Message = "No task IDs provided." };
        }

        var tasks = await _dbContext.Tasks
            .Include(t => t.Attachments)
            .Include(t => t.ActivityLogs)
            .Where(t => taskIds.Contains(t.Id))
            .ToListAsync(cancellationToken);

        if (tasks.Count == 0)
        {
            return new BulkDeleteTasksResponse { DeletedCount = 0, Message = "No matching tasks found." };
        }

        foreach (var task in tasks)
        {
            if (task.ActivityLogs != null && task.ActivityLogs.Any())
            {
                _dbContext.AppActivityLogs.RemoveRange(task.ActivityLogs);
            }
            if (task.Attachments != null && task.Attachments.Any())
            {
                _dbContext.TaskAttachments.RemoveRange(task.Attachments);
            }
        }

        _dbContext.Tasks.RemoveRange(tasks);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = "ALL",
            EventType = "TASKS_BULK_DELETED",
            Details = $"Bulk removed {tasks.Count} tasks",
            TaskCode = $"{tasks.Count} TASKS"
        });

        return new BulkDeleteTasksResponse
        {
            DeletedCount = tasks.Count,
            Message = $"Successfully deleted {tasks.Count} tasks."
        };
    }

    public async Task<DeleteAllTasksResponse> DeleteAllTasksAsync(Guid deletedBy, string deletedByName, CancellationToken cancellationToken = default)
    {
        var allTasks = await _dbContext.Tasks
            .Include(t => t.Attachments)
            .Include(t => t.ActivityLogs)
            .ToListAsync(cancellationToken);

        if (allTasks.Count == 0)
        {
            return new DeleteAllTasksResponse
            {
                DeletedCount = 0,
                BackupId = Guid.Empty,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                Message = "No tasks exist in the system to delete."
            };
        }

        // Prepare backup payload
        var backupDtos = allTasks.Select(MapToDto).ToList();
        var backupJson = System.Text.Json.JsonSerializer.Serialize(backupDtos);

        var now = DateTime.UtcNow;
        var expiresAt = now.AddDays(30);

        var backup = new TaskBackup
        {
            Id = Guid.NewGuid(),
            TaskCount = allTasks.Count,
            TasksJson = backupJson,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            DeletedBy = deletedBy,
            DeletedByName = deletedByName,
            IsRestored = false,
            DismissedDaysJson = "[]"
        };

        _dbContext.TaskBackups.Add(backup);

        // Remove all task activity logs and attachments
        foreach (var task in allTasks)
        {
            if (task.ActivityLogs != null && task.ActivityLogs.Any())
            {
                _dbContext.AppActivityLogs.RemoveRange(task.ActivityLogs);
            }
            if (task.Attachments != null && task.Attachments.Any())
            {
                _dbContext.TaskAttachments.RemoveRange(task.Attachments);
            }
        }

        _dbContext.Tasks.RemoveRange(allTasks);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = "ALL",
            EventType = "TASKS_PURGED_BACKED_UP",
            Details = $"All {allTasks.Count} tasks deleted and backed up for 30 days (Backup ID: {backup.Id})",
            TaskCode = "ALL_TASKS"
        });

        return new DeleteAllTasksResponse
        {
            DeletedCount = allTasks.Count,
            BackupId = backup.Id,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            Message = $"All {allTasks.Count} tasks successfully deleted and backed up for 30 days."
        };
    }

    public async Task<List<TaskBackupDto>> GetTaskBackupsAsync(CancellationToken cancellationToken = default)
    {
        var backups = await _dbContext.TaskBackups
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var list = new List<TaskBackupDto>();

        foreach (var b in backups)
        {
            var elapsedDays = (int)Math.Floor((now - b.CreatedAt).TotalDays);
            var remainingDays = Math.Max(0, (int)Math.Ceiling((b.ExpiresAt - now).TotalDays));
            var isExpired = now > b.ExpiresAt;

            // Determine if an active reminder milestone is triggered
            int? activeMilestone = null;
            if (!b.IsRestored && !isExpired)
            {
                var dismissed = new HashSet<int>();
                try
                {
                    if (!string.IsNullOrWhiteSpace(b.DismissedDaysJson))
                    {
                        var parsed = System.Text.Json.JsonSerializer.Deserialize<List<int>>(b.DismissedDaysJson);
                        if (parsed != null) dismissed = new HashSet<int>(parsed);
                    }
                }
                catch { }

                // Check highest reached milestone that has not been dismissed
                foreach (var m in ReminderMilestoneDays)
                {
                    if (elapsedDays >= m && !dismissed.Contains(m))
                    {
                        activeMilestone = m;
                    }
                }
            }

            list.Add(new TaskBackupDto
            {
                Id = b.Id,
                TaskCount = b.TaskCount,
                CreatedAt = b.CreatedAt,
                ExpiresAt = b.ExpiresAt,
                ElapsedDays = elapsedDays,
                RemainingDays = remainingDays,
                DeletedByName = b.DeletedByName,
                IsRestored = b.IsRestored,
                RestoredAt = b.RestoredAt,
                IsExpired = isExpired,
                ActiveReminderMilestone = activeMilestone
            });
        }

        return list;
    }

    public async Task<RestoreBackupResponse?> RestoreTaskBackupAsync(Guid backupId, Guid restoredBy, CancellationToken cancellationToken = default)
    {
        var backup = await _dbContext.TaskBackups
            .FirstOrDefaultAsync(b => b.Id == backupId, cancellationToken);
        if (backup == null) return null;

        List<TaskResponseDto>? tasksToRestore = null;
        try
        {
            tasksToRestore = System.Text.Json.JsonSerializer.Deserialize<List<TaskResponseDto>>(backup.TasksJson);
        }
        catch { }

        if (tasksToRestore == null || tasksToRestore.Count == 0)
        {
            return new RestoreBackupResponse
            {
                BackupId = backupId,
                RestoredCount = 0,
                Message = "No tasks found in backup snapshot."
            };
        }

        var existingTaskCodes = (await _dbContext.Tasks.Select(t => t.TaskCode).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var restoredTasks = new List<TaskItem>();
        int codeCounter = 101;

        foreach (var dto in tasksToRestore)
        {
            var code = dto.TaskCode;
            while (string.IsNullOrWhiteSpace(code) || existingTaskCodes.Contains(code))
            {
                code = $"TASK-{codeCounter++}";
            }
            existingTaskCodes.Add(code);

            var task = new TaskItem
            {
                Id = Guid.NewGuid(),
                TaskCode = code,
                Title = dto.Title,
                Description = dto.Description,
                AssignedTo = dto.AssignedTo,
                AssignedBy = dto.AssignedBy ?? restoredBy,
                Status = dto.Status ?? TaskItemStatus.ASSIGNED.ToString(),
                Priority = dto.Priority ?? TaskPriority.MEDIUM.ToString(),
                DeliverableRequired = dto.DeliverableRequired,
                DeliverableUrl = dto.DeliverableUrl,
                DeliverableFileName = dto.DeliverableFileName,
                DueDate = dto.DueDate.HasValue ? DateTime.SpecifyKind(dto.DueDate.Value, DateTimeKind.Utc) : null,
                CreatedAt = DateTime.UtcNow
            };
            restoredTasks.Add(task);
        }

        _dbContext.Tasks.AddRange(restoredTasks);

        backup.IsRestored = true;
        backup.RestoredAt = DateTime.UtcNow;
        backup.RestoredBy = restoredBy;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = "ALL",
            EventType = "TASKS_RESTORED_FROM_BACKUP",
            Details = $"Restored {restoredTasks.Count} tasks from backup {backupId}",
            TaskCode = "RESTORE"
        });

        return new RestoreBackupResponse
        {
            BackupId = backupId,
            RestoredCount = restoredTasks.Count,
            Message = $"Successfully restored {restoredTasks.Count} tasks from backup."
        };
    }

    public async Task<bool> DismissBackupReminderAsync(Guid backupId, int milestoneDay, CancellationToken cancellationToken = default)
    {
        var backup = await _dbContext.TaskBackups.FirstOrDefaultAsync(b => b.Id == backupId, cancellationToken);
        if (backup == null) return false;

        var dismissed = new HashSet<int>();
        try
        {
            if (!string.IsNullOrWhiteSpace(backup.DismissedDaysJson))
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<int>>(backup.DismissedDaysJson);
                if (parsed != null) dismissed = new HashSet<int>(parsed);
            }
        }
        catch { }

        dismissed.Add(milestoneDay);
        backup.DismissedDaysJson = System.Text.Json.JsonSerializer.Serialize(dismissed);
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
