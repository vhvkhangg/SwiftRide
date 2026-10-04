using System;

namespace SwiftRide.TripService.Application.DTOs;

public sealed record DriverAcceptRequest(
    Guid DriverId
);
