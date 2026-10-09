using SwiftRide.MatchingService.Domain.Models;

namespace SwiftRide.MatchingService.Domain.Interfaces;

public interface IPricingStrategy
{
    decimal CalculatePrice(PricingContext context);
}
