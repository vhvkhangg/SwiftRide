namespace SwiftRide.PaymentService.Application.Integrations;

public sealed record TripSnapshot(
    Guid Id,
    Guid RiderId,
    Guid? DriverId,
    decimal? FareEstimate,
    TripStatusCode Status
);