using Microsoft.AspNetCore.SignalR;
using PHF.Core.DTOs;

namespace PHF.Realtime.Hubs;

public class WorkstationHub : Hub
{
    public const string ManagerGroup = "Managers";

    public async Task JoinStation(string stationId)
    {
        if (!string.IsNullOrWhiteSpace(stationId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Station_{stationId}");
        }
    }

    public async Task LeaveStation(string stationId)
    {
        if (!string.IsNullOrWhiteSpace(stationId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Station_{stationId}");
        }
    }

    public async Task JoinUserGroup(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }
    }

    public async Task JoinManagerFeed()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, ManagerGroup);
    }

    public async Task LeaveManagerFeed()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ManagerGroup);
    }
}
