using Microsoft.AspNetCore.SignalR;
using SwiftRide.TripService.Api;
using SwiftRide.TripService.Application.DTOs;
using SwiftRide.TripService.Application.Interfaces;

namespace SwiftRide.TripService.Infrastructure;

public sealed class SignalRTripNotifier(IHubContext<TripNotificationHub> hub) : ITripNotifier
{
    public Task NotifyDriverInvitationAsync(
        Guid driverId,
        Guid tripId,
        string pickupAddress,
        string dropoffAddress,
        decimal? fareEstimate,
        DateTime expiresAtUtc,
        CancellationToken ct) =>
        hub.Clients.Group(TripNotificationGroups.ForUser(driverId))
            .SendAsync("driverInvitation", new
            {
                tripId,
                pickupAddress,
                dropoffAddress,
                fareEstimate,
                expiresAtUtc
            }, ct);

    public Task NotifyTripChangedAsync(TripResponse trip, CancellationToken ct)
    {
        var recipients = trip.DriverId is Guid driverId
            ? new[] { trip.RiderId, driverId }
            : new[] { trip.RiderId };

        return hub.Clients.Groups(recipients.Select(TripNotificationGroups.ForUser).ToArray())
            .SendAsync("tripChanged", trip, ct);
    }
}
