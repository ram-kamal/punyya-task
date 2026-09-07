using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface IStaffService
{
    Task<IEnumerable<StaffMemberDto>> GetAllStaffAsync(CancellationToken cancellationToken = default);
    Task<StaffMemberDto?> GetStaffByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StaffMemberDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken cancellationToken = default);
    Task<bool> ToggleActiveStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> UpdateWorkModeAsync(Guid userId, string workMode, CancellationToken cancellationToken = default);
}
