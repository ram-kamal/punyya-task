using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface ILeaveService
{
    Task<LeaveResponseDto> ApplyLeaveAsync(CreateLeaveRequestDto request, Guid userId, CancellationToken ct = default);
    Task<List<LeaveResponseDto>> GetMyLeavesAsync(Guid userId, CancellationToken ct = default);
    Task<List<LeaveResponseDto>> GetAllLeavesAsync(string? status = null, CancellationToken ct = default);
    Task<LeaveResponseDto?> ReviewLeaveAsync(Guid leaveId, string action, string? comments, Guid reviewedBy, CancellationToken ct = default);
    Task<LeaveResponseDto?> GetLeaveByIdAsync(Guid leaveId, CancellationToken ct = default);
}
