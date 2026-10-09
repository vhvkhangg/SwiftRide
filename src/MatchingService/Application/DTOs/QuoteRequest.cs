namespace SwiftRide.MatchingService.Application.DTOs;

public sealed record QuoteRequest(
    decimal Distance,
    decimal EstimatedTime,
    string? RideType,
    decimal Surge,
    string? PromoCode);
