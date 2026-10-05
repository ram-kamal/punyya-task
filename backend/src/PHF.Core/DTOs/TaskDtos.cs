using System.ComponentModel.DataAnnotations;
using PHF.Core.Enums;

namespace PHF.Core.DTOs;

public record CreateTaskRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public Guid? AssignedTo { get; init; }

    public string Priority { get; init; } = TaskPriority.MEDIUM.ToString();

    public bool DeliverableRequired { get; init; } = true;

    public DateTime? DueDate { get; init; }
}

public record UpdateTaskRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public Guid? AssignedTo { get; init; }
    public string? Priority { get; init; }
    public DateTime? DueDate { get; init; }
    public bool? DeliverableRequired { get; init; }
    public string? Status { get; init; }
}

public record UpdateTaskStatusRequest
{
    [Required]
    public string Status { get; init; } = string.Empty;

    public string? DeliverableUrl { get; init; }

    public string? DeliverableFileName { get; init; }
}

public record AttachDeliverableRequest
{
    [Required]
    public Guid TaskId { get; init; }

    [Required]
    public string DeliverableUrl { get; init; } = string.Empty;

    [Required]
    public string FileName { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    public string? MimeType { get; init; }
}

public record AttachDeliverableLinkRequest
{
    [Required]
    public string Url { get; init; } = string.Empty;

    public string? FileName { get; init; }
}

public record TaskAttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string StoragePath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string? MimeType { get; init; }
    public DateTime UploadedAt { get; init; }
}

public record TaskResponseDto
{
    public Guid Id { get; init; }
    public string TaskCode { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? AssignedTo { get; init; }
    public string? AssignedToName { get; init; }
    public string? AssignedToStation { get; init; }
    public Guid? AssignedBy { get; init; }
    public string? AssignedByName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public bool DeliverableRequired { get; init; }
    public string? DeliverableUrl { get; init; }
    public string? DeliverableFileName { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public int TotalTrackedSeconds { get; init; }
    public List<TaskAttachmentDto> Attachments { get; init; } = new();
}

public record BulkDeleteTasksRequest
{
    [Required]
    public List<Guid> TaskIds { get; init; } = new();
}

public record BulkDeleteTasksResponse
{
    public int DeletedCount { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record DeleteAllTasksResponse
{
    public int DeletedCount { get; init; }
    public Guid BackupId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record TaskBackupDto
{
    public Guid Id { get; init; }
    public int TaskCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int ElapsedDays { get; init; }
    public int RemainingDays { get; init; }
    public string? DeletedByName { get; init; }
    public bool IsRestored { get; init; }
    public DateTime? RestoredAt { get; init; }
    public bool IsExpired { get; init; }
    public int? ActiveReminderMilestone { get; init; }
}

public record RestoreBackupResponse
{
    public Guid BackupId { get; init; }
    public int RestoredCount { get; init; }
    public string Message { get; init; } = string.Empty;
}

public record DismissReminderRequest
{
    public int MilestoneDay { get; init; }
}
