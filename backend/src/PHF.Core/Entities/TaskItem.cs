using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PHF.Core.Enums;

namespace PHF.Core.Entities;

[Table("tasks")]
public class TaskItem
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    [Column("task_code")]
    public string TaskCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("assigned_to")]
    public Guid? AssignedTo { get; set; }

    [ForeignKey(nameof(AssignedTo))]
    public User? AssignedToUser { get; set; }

    [Column("assigned_by")]
    public Guid? AssignedBy { get; set; }

    [ForeignKey(nameof(AssignedBy))]
    public User? AssignedByUser { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = TaskItemStatus.ASSIGNED.ToString();

    [Required]
    [MaxLength(20)]
    [Column("priority")]
    public string Priority { get; set; } = TaskPriority.MEDIUM.ToString();

    [Column("deliverable_required")]
    public bool DeliverableRequired { get; set; } = true;

    [Column("deliverable_url")]
    public string? DeliverableUrl { get; set; }

    [MaxLength(255)]
    [Column("deliverable_file_name")]
    public string? DeliverableFileName { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("started_at")]
    public DateTime? StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Attachments
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public ICollection<AppActivityLog> ActivityLogs { get; set; } = new List<AppActivityLog>();
}
