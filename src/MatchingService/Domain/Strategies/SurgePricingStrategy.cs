using SwiftRide.MatchingService.Domain.Interfaces;
using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Domain.Strategies;

public sealed class SurgePricingStrategy : IPricingStrategy
{
    public decimal CalculatePrice(PricingContext context)
    {
        var surge = Math.Max(1m, context.SurgeMultiplier);
        var operating = context.CostPerKm * context.DistanceKm + context.CostPerMinute * context.TimeMinutes;
        return Math.Max(0m, (operating * surge + context.Toll) * context.PromotionFactor * (1m + context.VatRate));
    }
}
