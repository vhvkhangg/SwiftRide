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
    decimal Surge = 1m,
    string? PromoCode = null,
    string? IdempotencyKey = null 
);
