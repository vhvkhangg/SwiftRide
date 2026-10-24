using SwiftRide.TripService.Application.DTOs;

namespace SwiftRide.TripService.Application.Interfaces;

public interface ITripNotifier
{
    Task NotifyDriverInvitationAsync(
        Guid driverId,
        Guid tripId,
        string pickupAddress,
        string dropoffAddress,
        decimal? fareEstimate,
        DateTime expiresAtUtc,
        CancellationToken ct);

    Task NotifyTripChangedAsync(TripResponse trip, CancellationToken ct);
}
