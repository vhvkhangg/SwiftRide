using SwiftRide.MatchingService.Application.DTOs;

namespace SwiftRide.MatchingService.Application.Interfaces;

public interface IPricingService
{
    QuoteResponse Calculate(QuoteRequest request);
}
