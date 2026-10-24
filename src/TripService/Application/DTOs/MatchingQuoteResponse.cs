namespace SwiftRide.TripService.Application.DTOs;

public sealed record MatchingQuoteResponse(
    decimal Base,
    decimal Distance,
    decimal Time,
    decimal Surge,
    decimal Toll,
    decimal Discount,
    decimal Tax,
    decimal Total);
