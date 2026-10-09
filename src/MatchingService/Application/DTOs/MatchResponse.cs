namespace SwiftRide.MatchingService.Application.DTOs;

public sealed record MatchResponse(
    string TripId,
    IReadOnlyList<MatchedDriver> Drivers);

public sealed record MatchedDriver(
    string DriverId,
    double Latitude,
    double Longitude,
    double DistanceKm,
    DateTimeOffset UpdatedAt);
