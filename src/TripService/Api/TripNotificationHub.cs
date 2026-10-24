using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SwiftRide.Security;

namespace SwiftRide.TripService.Api;

[Authorize]
public sealed class TripNotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.UserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, TripNotificationGroups.ForUser(userId));
        await base.OnConnectedAsync();
    }
}

public static class TripNotificationGroups
{
    public static string ForUser(Guid userId) => $"user:{userId:D}";
}
