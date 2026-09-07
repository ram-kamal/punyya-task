using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PHF.Core.Entities;

[Table("user_sessions")]
public class UserSession
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("machine_name")]
    public string MachineName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty; // LOGON, LOGOFF, LOCK, UNLOCK

    [Column("event_timestamp")]
    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
