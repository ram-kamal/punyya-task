using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PHF.Core.Enums;

namespace PHF.Core.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("username")]
    public string? Username { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    [Column("role")]
    public string Role { get; set; } = UserRole.EMPLOYEE.ToString();

    [MaxLength(50)]
    [Column("station_id")]
    public string? StationId { get; set; }

    [MaxLength(50)]
    [Column("work_mode")]
    public string WorkMode { get; set; } = "IN_OFFICE";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<UserSession> UserSessions { get; set; } = new List<UserSession>();
    public ICollection<AppActivityLog> AppActivityLogs { get; set; } = new List<AppActivityLog>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
}
