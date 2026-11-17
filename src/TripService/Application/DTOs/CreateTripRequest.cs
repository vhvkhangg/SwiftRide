using System;

namespace SwiftRide.TripService.Application.DTOs;

public sealed record CreateTripRequest(
    Guid RiderId,
    string PickupAddress,
    string DropoffAddress,
    double? PickupLatitude,
    double? PickupLongitude,
    decimal Distance,
    decimal EstimatedTime,
    string? RideType = null,
    string? PromoCode = null,
    string? IdempotencyKey = null,
    // Optional: lets apps draw the route on a map. Not used for pricing (distance/time are sent above).
    double? DropoffLatitude = null,
    double? DropoffLongitude = null
);
