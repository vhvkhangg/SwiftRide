using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Domain.Strategies;

public sealed class StandardPricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(PricingContext context)
    {
        var operating = context.CostPerKm * context.DistanceKm + context.CostPerMinute * context.TimeMinutes;
        return Math.Max(0m, (operating * context.SurgeMultiplier + context.Toll) * context.PromotionFactor * (1m + context.VatRate));
    }
}
