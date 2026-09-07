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
