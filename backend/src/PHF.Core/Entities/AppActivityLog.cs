using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PHF.Core.Entities;

[Table("app_activity_logs")]
public class AppActivityLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Column("task_id")]
    public Guid? TaskId { get; set; }

    [ForeignKey(nameof(TaskId))]
    public TaskItem? Task { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("process_name")]
    public string ProcessName { get; set; } = string.Empty;

    [Column("window_title")]
    public string? WindowTitle { get; set; }

    [Column("duration_seconds")]
    public int DurationSeconds { get; set; }

    [Column("is_idle")]
    public bool IsIdle { get; set; } = false;

    [Column("logged_at")]
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
