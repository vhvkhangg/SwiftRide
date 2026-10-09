using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Domain.Strategies;

public sealed class PromoPricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(PricingContext context)
    {
        var factor = Math.Clamp(context.PromotionFactor, 0m, 1m);
        var operating = context.CostPerKm * context.DistanceKm + context.CostPerMinute * context.TimeMinutes;
        return Math.Max(0m, (operating * context.SurgeMultiplier + context.Toll) * factor * (1m + context.VatRate));
    }
}
