using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PHF.Core.Entities;

[Table("task_backups")]
public class TaskBackup
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("task_count")]
    public int TaskCount { get; set; }

    [Required]
    [Column("tasks_json")]
    public string TasksJson { get; set; } = "[]";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);

    [Column("deleted_by")]
    public Guid? DeletedBy { get; set; }

    [MaxLength(255)]
    [Column("deleted_by_name")]
    public string? DeletedByName { get; set; }

    [Column("is_restored")]
    public bool IsRestored { get; set; } = false;

    [Column("restored_at")]
    public DateTime? RestoredAt { get; set; }

    [Column("restored_by")]
    public Guid? RestoredBy { get; set; }

    [Column("dismissed_days_json")]
    public string DismissedDaysJson { get; set; } = "[]";
}
