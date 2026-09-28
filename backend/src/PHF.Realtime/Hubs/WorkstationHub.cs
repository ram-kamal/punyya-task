using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PHF.Core.DTOs;

namespace PHF.Realtime.Hubs;

[Authorize]
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
        if (string.IsNullOrWhiteSpace(userId)) return;

        var currentUserId = Context.UserIdentifier ?? Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAuthorized = Context.User?.IsInRole("ADMIN") == true 
                        || Context.User?.IsInRole("MANAGER") == true 
                        || Context.User?.IsInRole("MONITOR") == true;

        if (!isAuthorized && !string.Equals(currentUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            throw new HubException("Access denied to user feed.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
    }

    public async Task JoinManagerFeed()
    {
        var isAuthorized = Context.User?.IsInRole("ADMIN") == true 
                        || Context.User?.IsInRole("MANAGER") == true 
                        || Context.User?.IsInRole("MONITOR") == true;
        if (!isAuthorized)
        {
            throw new HubException("Access denied. Admin, Manager, or Monitor role required.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, ManagerGroup);
    }

    public async Task LeaveManagerFeed()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ManagerGroup);
    }
}
