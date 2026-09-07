using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PHF.Core.Enums;

namespace PHF.Core.Entities;

[Table("shifts")]
public class Shift
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("station_id")]
    public string StationId { get; set; } = string.Empty;

    [Column("clock_in")]
    public DateTime ClockIn { get; set; } = DateTime.UtcNow;

    [Column("clock_out")]
    public DateTime? ClockOut { get; set; }

    [Column("total_active_seconds")]
    public int TotalActiveSeconds { get; set; } = 0;

    [Column("total_idle_seconds")]
    public int TotalIdleSeconds { get; set; } = 0;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = ShiftStatus.ACTIVE.ToString();

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
