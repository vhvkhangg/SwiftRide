namespace SwiftRide.MatchingService.Domain.Models;

public sealed record PricingContext(
    decimal DistanceKm,
    decimal TimeMinutes,
    decimal CostPerKm,
    decimal CostPerMinute,
    decimal SurgeMultiplier,
    decimal Toll,
    decimal PromotionFactor,
    decimal VatRate);
