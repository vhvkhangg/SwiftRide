namespace SwiftRide.TripService.Application.DTOs;

public sealed record MatchingQuoteRequest(
    decimal Distance,
    decimal EstimatedTime,
    string? RideType,
    string? PromoCode);
