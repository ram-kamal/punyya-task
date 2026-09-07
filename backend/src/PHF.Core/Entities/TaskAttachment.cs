using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PHF.Core.Entities;

[Table("task_attachments")]
public class TaskAttachment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("task_id")]
    public Guid TaskId { get; set; }

    [ForeignKey(nameof(TaskId))]
    public TaskItem? Task { get; set; }

    [Column("uploaded_by")]
    public Guid? UploadedBy { get; set; }

    [ForeignKey(nameof(UploadedBy))]
    public User? UploadedByUser { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [Column("storage_path")]
    public string StoragePath { get; set; } = string.Empty;

    [Column("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [MaxLength(100)]
    [Column("mime_type")]
    public string? MimeType { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
