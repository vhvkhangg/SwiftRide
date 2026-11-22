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
    DateTime? UpdatedAt,
    string? PaymentFailureReason,
    int DeclineCount = 0,
    int? DeclineThreshold = null,
    double? PickupLatitude = null,
    double? PickupLongitude = null,
    double? DropoffLatitude = null,
    double? DropoffLongitude = null,
    string PaymentMethod = "Wallet"
);
