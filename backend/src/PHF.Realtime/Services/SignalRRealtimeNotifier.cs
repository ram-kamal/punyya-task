using Microsoft.AspNetCore.SignalR;
using PHF.Core.DTOs;
using PHF.Core.Interfaces;
using PHF.Realtime.Hubs;

namespace PHF.Realtime.Services;

public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<WorkstationHub> _hubContext;

    public SignalRRealtimeNotifier(IHubContext<WorkstationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastWorkstationEventAsync(LiveWorkstationEventDto eventDto)
    {
        // Broadcast to manager dashboard live feed
        await _hubContext.Clients.Group(WorkstationHub.ManagerGroup)
            .SendAsync("WorkstationEventReceived", eventDto);

        // Also broadcast to station group if targeted
        if (!string.IsNullOrWhiteSpace(eventDto.StationId))
        {
            await _hubContext.Clients.Group($"Station_{eventDto.StationId}")
                .SendAsync("WorkstationEventReceived", eventDto);
        }
    }

    public async Task BroadcastWorkstationStatusAsync(WorkstationStatusDto statusDto)
    {
        await _hubContext.Clients.Group(WorkstationHub.ManagerGroup)
            .SendAsync("WorkstationStatusUpdated", statusDto);
    }

    public async Task NotifyTaskAssignedAsync(Guid userId, TaskResponseDto taskDto)
    {
        // Notify specific user
        await _hubContext.Clients.Group($"User_{userId}")
            .SendAsync("TaskAssigned", taskDto);

        // If station assigned
        if (!string.IsNullOrWhiteSpace(taskDto.AssignedToStation))
        {
            await _hubContext.Clients.Group($"Station_{taskDto.AssignedToStation}")
                .SendAsync("TaskAssigned", taskDto);
        }

        // Also notify managers
        await _hubContext.Clients.Group(WorkstationHub.ManagerGroup)
            .SendAsync("TaskAssigned", taskDto);
    }

    public async Task NotifyTaskStatusChangedAsync(Guid taskId, string newStatus, string? deliverableUrl)
    {
        await _hubContext.Clients.All
            .SendAsync("TaskStatusChanged", new { TaskId = taskId, NewStatus = newStatus, DeliverableUrl = deliverableUrl });
    }
}
