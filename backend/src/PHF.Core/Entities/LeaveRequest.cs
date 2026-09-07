using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PHF.Core.Enums;

namespace PHF.Core.Entities;

[Table("leave_requests")]
public class LeaveRequest
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
    [Column("leave_type")]
    public string LeaveType { get; set; } = Enums.LeaveType.CASUAL.ToString();

    [Required]
    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Required]
    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("total_days")]
    public int TotalDays { get; set; } = 1;

    [Required]
    [MaxLength(1000)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("status")]
    public string Status { get; set; } = LeaveStatus.PENDING.ToString();

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [ForeignKey(nameof(ReviewedBy))]
    public User? Reviewer { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [MaxLength(1000)]
    [Column("review_comments")]
    public string? ReviewComments { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
