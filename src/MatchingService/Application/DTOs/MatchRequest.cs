namespace SwiftRide.MatchingService.Application.DTOs;

public sealed record MatchRequest(
    string TripId,
    double PickupLatitude,
    double PickupLongitude,
    double RadiusKm);
