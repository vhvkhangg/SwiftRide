using System;

namespace SwiftRide.TripService.Application.DTOs;

public sealed record CreateTripRequest(
    Guid RiderId,
    string PickupAddress,
    string DropoffAddress,
    decimal? FareEstimate
);
