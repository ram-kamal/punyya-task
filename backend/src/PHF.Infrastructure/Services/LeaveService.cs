using Microsoft.EntityFrameworkCore;
using PHF.Core.DTOs;
using PHF.Core.Entities;
using PHF.Core.Enums;
using PHF.Core.Interfaces;
using PHF.Infrastructure.Data;

namespace PHF.Infrastructure.Services;

public class LeaveService : ILeaveService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public LeaveService(ApplicationDbContext dbContext, IRealtimeNotifier realtimeNotifier)
    {
        _dbContext = dbContext;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<LeaveResponseDto> ApplyLeaveAsync(
        CreateLeaveRequestDto request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        if (request.EndDate.Date < request.StartDate.Date)
        {
            throw new ArgumentException("End date cannot be earlier than start date.");
        }

        var user = await _dbContext.Users.FindAsync(new object[] { userId }, ct)
            ?? throw new KeyNotFoundException($"User with ID {userId} not found.");

        var totalDays = (int)(request.EndDate.Date - request.StartDate.Date).TotalDays + 1;
        if (totalDays <= 0) totalDays = 1;

        var leaveType = !string.IsNullOrWhiteSpace(request.LeaveType) 
            ? request.LeaveType.ToUpperInvariant() 
            : LeaveType.CASUAL.ToString();

        var leave = new LeaveRequest
        {
            UserId = userId,
            LeaveType = leaveType,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            TotalDays = totalDays,
            Reason = request.Reason.Trim(),
            Status = LeaveStatus.PENDING.ToString(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.LeaveRequests.Add(leave);
        await _dbContext.SaveChangesAsync(ct);

        // Broadcast real-time activity event
        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = user.StationId ?? "ALL",
            UserName = user.FullName,
            EventType = "LEAVE_SUBMITTED",
            Details = $"{user.FullName} applied for {leave.LeaveType} leave ({leave.TotalDays} day{(leave.TotalDays > 1 ? "s" : "")})"
        });

        return MapToDto(leave, user.FullName, user.Email, user.StationId);
    }

    public async Task<List<LeaveResponseDto>> GetMyLeavesAsync(Guid userId, CancellationToken ct = default)
    {
        var leaves = await _dbContext.LeaveRequests
            .Include(l => l.User)
            .Include(l => l.Reviewer)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return leaves.Select(l => MapToDto(l, l.User?.FullName ?? "", l.User?.Email ?? "", l.User?.StationId, l.Reviewer?.FullName)).ToList();
    }

    public async Task<List<LeaveResponseDto>> GetAllLeavesAsync(string? status = null, CancellationToken ct = default)
    {
        var query = _dbContext.LeaveRequests
            .Include(l => l.User)
            .Include(l => l.Reviewer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var targetStatus = status.ToUpperInvariant();
            query = query.Where(l => l.Status == targetStatus);
        }

        var leaves = await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return leaves.Select(l => MapToDto(l, l.User?.FullName ?? "", l.User?.Email ?? "", l.User?.StationId, l.Reviewer?.FullName)).ToList();
    }

    public async Task<LeaveResponseDto?> ReviewLeaveAsync(
        Guid leaveId, 
        string action, 
        string? comments, 
        Guid reviewedBy, 
        CancellationToken ct = default)
    {
        var leave = await _dbContext.LeaveRequests
            .Include(l => l.User)
            .Include(l => l.Reviewer)
            .FirstOrDefaultAsync(l => l.Id == leaveId, ct);

        if (leave == null) return null;

        var reviewer = await _dbContext.Users.FindAsync(new object[] { reviewedBy }, ct);

        var isApprove = action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase) || 
                        action.Equals("APPROVED", StringComparison.OrdinalIgnoreCase);

        leave.Status = isApprove ? LeaveStatus.APPROVED.ToString() : LeaveStatus.REJECTED.ToString();
        leave.ReviewedBy = reviewedBy;
        leave.ReviewedAt = DateTime.UtcNow;
        leave.ReviewComments = comments?.Trim();

        await _dbContext.SaveChangesAsync(ct);

        // Broadcast real-time activity event
        await _realtimeNotifier.BroadcastWorkstationEventAsync(new LiveWorkstationEventDto
        {
            StationId = leave.User?.StationId ?? "ALL",
            UserName = leave.User?.FullName,
            EventType = "LEAVE_STATUS_CHANGED",
            Details = $"Leave for {leave.User?.FullName} was {leave.Status} by {reviewer?.FullName ?? "Manager"}"
        });

        return MapToDto(leave, leave.User?.FullName ?? "", leave.User?.Email ?? "", leave.User?.StationId, reviewer?.FullName);
    }

    public async Task<LeaveResponseDto?> GetLeaveByIdAsync(Guid leaveId, CancellationToken ct = default)
    {
        var leave = await _dbContext.LeaveRequests
            .Include(l => l.User)
            .Include(l => l.Reviewer)
            .FirstOrDefaultAsync(l => l.Id == leaveId, ct);

        if (leave == null) return null;

        return MapToDto(leave, leave.User?.FullName ?? "", leave.User?.Email ?? "", leave.User?.StationId, leave.Reviewer?.FullName);
    }

    private static LeaveResponseDto MapToDto(
        LeaveRequest l, 
        string userName, 
        string userEmail, 
        string? stationId, 
        string? reviewerName = null)
    {
        return new LeaveResponseDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = userName,
            UserEmail = userEmail,
            StationId = stationId,
            LeaveType = l.LeaveType,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            TotalDays = l.TotalDays,
            Reason = l.Reason,
            Status = l.Status,
            ReviewedBy = l.ReviewedBy,
            ReviewerName = reviewerName,
            ReviewedAt = l.ReviewedAt,
            ReviewComments = l.ReviewComments,
            CreatedAt = l.CreatedAt
        };
    }
}
