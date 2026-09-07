using PHF.Core.DTOs;

namespace PHF.Core.Interfaces;

public interface IRealtimeNotifier
{
    Task BroadcastWorkstationEventAsync(LiveWorkstationEventDto eventDto);
    Task BroadcastWorkstationStatusAsync(WorkstationStatusDto statusDto);
    Task NotifyTaskAssignedAsync(Guid userId, TaskResponseDto taskDto);
    Task NotifyTaskStatusChangedAsync(Guid taskId, string newStatus, string? deliverableUrl);
}
