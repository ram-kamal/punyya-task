using System.ComponentModel.DataAnnotations;

namespace PHF.Core.DTOs;

public class CreateLeaveRequestDto
{
    [Required]
    public string LeaveType { get; set; } = "CASUAL";

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class ReviewLeaveRequestDto
{
    [MaxLength(1000)]
    public string? Comments { get; set; }
}

public class LeaveResponseDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string? StationId { get; set; }
    public string LeaveType { get; set; } = "CASUAL";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public Guid? ReviewedBy { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComments { get; set; }
    public DateTime CreatedAt { get; set; }
}
