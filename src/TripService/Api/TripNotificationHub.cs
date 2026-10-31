using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SwiftRide.Security;
using Microsoft.EntityFrameworkCore;
using SwiftRide.TripService.Infrastructure;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Api;

[Authorize]
public sealed class TripNotificationHub(TripDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.UserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, TripNotificationGroups.ForUser(userId));
        if (Context.User!.IsInRole("Driver"))
        {
            var now = DateTime.UtcNow;
            var invitations = await db.DriverInvitations.AsNoTracking()
                .Where(invitation => invitation.DriverId == userId && !invitation.IsResolved &&
                    invitation.ExpiresAtUtc > now && invitation.Trip.Status == TripStatus.PriceConfirmed &&
                    invitation.Trip.MatchingAttempt == invitation.Attempt)
                .Select(invitation => new
                {
                    TripId = invitation.TripId,
                    invitation.Trip.PickupAddress,
                    invitation.Trip.DropoffAddress,
                    invitation.Trip.FareEstimate,
                    invitation.ExpiresAtUtc
                }).ToListAsync(Context.ConnectionAborted);
            foreach (var invitation in invitations)
                await Clients.Caller.SendAsync("driverInvitation", invitation, Context.ConnectionAborted);
        }
        await base.OnConnectedAsync();
    }
}

public static class TripNotificationGroups
{
    public static string ForUser(Guid userId) => $"user:{userId:D}";
}
