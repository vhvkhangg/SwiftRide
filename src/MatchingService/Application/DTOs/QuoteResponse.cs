namespace SwiftRide.MatchingService.Application.DTOs;

public sealed record QuoteResponse(
    decimal Base,
    decimal Distance,
    decimal Time,
    decimal Surge,
    decimal Toll,
    decimal Discount,
    decimal Tax,
    decimal Total);
