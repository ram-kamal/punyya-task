using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface IShiftService
{
    Task<ShiftResponseDto> ClockInAsync(Guid userId, string stationId, CancellationToken cancellationToken = default);
    Task<ShiftResponseDto?> ClockOutAsync(Guid userId, string stationId, CancellationToken cancellationToken = default);
    Task<ShiftResponseDto?> GetCurrentShiftAsync(Guid userId, CancellationToken cancellationToken = default);
}
