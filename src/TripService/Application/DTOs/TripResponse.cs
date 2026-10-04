using System;
using SwiftRide.TripService.Domain.Enums;

namespace SwiftRide.TripService.Application.DTOs;

public sealed record TripResponse(
    Guid Id,
    Guid RiderId,
    Guid? DriverId,
    string PickupAddress,
    string DropoffAddress,
    decimal? FareEstimate,
    TripStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
